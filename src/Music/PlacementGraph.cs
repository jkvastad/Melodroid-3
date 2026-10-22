namespace Melodroid_3.Music;

// One distinct key set in the placement universe. Keyboard-coincident placements (same keys under
// renormalization / rounding) fold to a single node: Representative is the power-of-2-preferred, then
// lowest-LCM, then lowest-anchor placement (the collapse-isomorphic convention used elsewhere), and the
// rest are kept as Aliases for the ≅ label (e.g. 4@8 ≅ 3@3).
public readonly record struct PlacementNode(
    Placement Representative,
    IReadOnlyList<int> Keys,
    IReadOnlyList<Placement> Aliases);

// A direct-adjacency (cover) edge of the key-set containment poset: node FromIndex ⊂ node ToIndex with
// no node strictly between them. DistinguishingKeys = keys(To) \ keys(From).
public readonly record struct CoverEdge(
    int FromIndex,
    int ToIndex,
    IReadOnlyList<int> DistinguishingKeys);

// A transposition class (necklace) of key sets. CanonicalKeys is the normal-order representative set;
// Representative is an example placement of that shape (power-of-2-preferred, then lowest LCM/anchor).
public readonly record struct ShapeNode(
    IReadOnlyList<int> CanonicalKeys,
    Placement Representative);

// One shape-to-shape relationship: a cover edge collapsed to its transposition class. From/To are a
// representative concrete placement pair realising the relationship; DistinguishingKeys are that pair's.
public readonly record struct ShapeEdge(
    int FromShapeIndex,
    int ToShapeIndex,
    Placement From,
    Placement To,
    IReadOnlyList<int> DistinguishingKeys);

public static class PlacementGraph
{
    // The universe of distinct placements: every family swept over every ktet anchor, with
    // keyboard-coincident placements (identical key sets) folded to one node. Sorted by key count,
    // then representative LCM, then anchor — so nodes are discovered smallest-shape first.
    public static IReadOnlyList<PlacementNode> BuildUniverse(
        IReadOnlyList<LcmFamily> families, int ktet)
    {
        var nodes = new List<PlacementNode>();
        foreach (var group in families
            .SelectMany(f => Placements.Sweep(f, ktet))
            .GroupBy(p => Signature(p.Keys)))
        {
            var ordered = group
                .OrderBy(p => IsPowerOf2(p.Lcm) ? 0 : 1)
                .ThenBy(p => p.Lcm)
                .ThenBy(p => p.At)
                .ToList();
            var keys = ordered[0].Keys.Distinct().OrderBy(k => k).ToList();
            nodes.Add(new PlacementNode(ordered[0], keys, ordered.Skip(1).ToList()));
        }

        return nodes
            .OrderBy(n => n.Keys.Count)
            .ThenBy(n => n.Representative.Lcm)
            .ThenBy(n => n.Representative.At)
            .ToList();
    }

    // Cover (direct-adjacency) edges over the key-set containment poset: keep a strict-subset pair only
    // when no other node lies strictly between its endpoints. Mirrors the Hasse reduction in
    // LcmFamilyGraphRenderer, specialised to key-set inclusion.
    public static IReadOnlyList<CoverEdge> CoverEdges(IReadOnlyList<PlacementNode> nodes)
    {
        var sets = nodes.Select(n => new HashSet<int>(n.Keys)).ToList();
        var edges = new List<CoverEdge>();

        for (var i = 0; i < nodes.Count; i++)
        {
            for (var j = 0; j < nodes.Count; j++)
            {
                if (i == j || !StrictSubset(sets[i], sets[j])) continue;

                var covers = true;
                for (var m = 0; m < nodes.Count; m++)
                {
                    if (m == i || m == j) continue;
                    if (StrictSubset(sets[i], sets[m]) && StrictSubset(sets[m], sets[j]))
                    {
                        covers = false;
                        break;
                    }
                }
                if (!covers) continue;

                var distinguishing = nodes[j].Keys
                    .Where(k => !sets[i].Contains(k))
                    .OrderBy(k => k)
                    .ToList();
                edges.Add(new CoverEdge(i, j, distinguishing));
            }
        }

        return edges;
    }

    // Index of the node whose key set equals the given keys, or -1 if absent.
    public static int FindNode(IReadOnlyList<PlacementNode> nodes, IReadOnlyCollection<int> keys)
    {
        var sig = Signature(keys);
        for (var i = 0; i < nodes.Count; i++)
            if (Signature(nodes[i].Keys) == sig) return i;
        return -1;
    }

    // Node indices whose key set contains rootKeys (the up-set / up-closure of the root, root included).
    // The extended placement graph's node scope.
    public static IReadOnlyList<int> UpSet(
        IReadOnlyList<PlacementNode> nodes, IReadOnlyCollection<int> rootKeys)
    {
        var root = new HashSet<int>(rootKeys);
        var result = new List<int>();
        for (var i = 0; i < nodes.Count; i++)
            if (root.IsSubsetOf(nodes[i].Keys)) result.Add(i);
        return result;
    }

    // The single placement graph's node scope: the root plus its direct successors (immediate supersets).
    public static IReadOnlyList<int> PlacementScope(
        IReadOnlyList<CoverEdge> edges, int rootIndex)
    {
        var scope = new List<int> { rootIndex };
        scope.AddRange(edges.Where(e => e.FromIndex == rootIndex).Select(e => e.ToIndex));
        return scope.Distinct().ToList();
    }

    // A DFS pre-order of scope from root, following in-scope edges and visiting each node's out-neighbours
    // in ascending target order (ascending index already reflects universe/discovery order, so smaller
    // shapes/LCMs branch first). This branch-groups the declaration order so Mermaid's dagre layout is
    // seeded near-planar, reducing crossing wires. Any scope member unreachable from root is appended in
    // input order (defensive — the graph is a connected up-set in practice).
    public static IReadOnlyList<int> SeedOrder(
        IReadOnlyCollection<int> scope,
        IEnumerable<(int From, int To)> edges,
        int root)
    {
        var inScope = new HashSet<int>(scope);
        var adjacency = new Dictionary<int, List<int>>();
        foreach (var (from, to) in edges)
        {
            if (!inScope.Contains(from) || !inScope.Contains(to)) continue;
            if (!adjacency.TryGetValue(from, out var successors))
                adjacency[from] = successors = new List<int>();
            successors.Add(to);
        }
        foreach (var successors in adjacency.Values) successors.Sort();

        var order = new List<int>();
        var visited = new HashSet<int>();

        void Visit(int node)
        {
            if (!visited.Add(node)) return;
            order.Add(node);
            if (adjacency.TryGetValue(node, out var successors))
                foreach (var next in successors) Visit(next);
        }

        if (inScope.Contains(root)) Visit(root);
        foreach (var node in scope) Visit(node);
        return order;
    }

    // Collapse the cover edges to shape-to-shape relationships: group nodes into shapes (transposition
    // classes via Chords.Canonicalize) and group cover edges by their ordered-pair necklace, keeping one
    // representative edge per class. No shape-level Hasse reduction — a direct adjacency like
    // tonic→triad must survive even though a tonic→dyad→… path exists.
    public static (IReadOnlyList<ShapeNode> Shapes, IReadOnlyList<ShapeEdge> Edges) ShapeGraph(
        IReadOnlyList<PlacementNode> nodes, IReadOnlyList<CoverEdge> edges, int ktet)
    {
        var shapeIndexBySig = new Dictionary<string, int>();
        var canonKeys = new List<IReadOnlyList<int>>();
        var representatives = new List<Placement>();

        int ShapeOf(PlacementNode node)
        {
            var canon = Chords.Canonicalize(node.Keys, ktet).Keys.OrderBy(k => k).ToList();
            var sig = Signature(canon);
            if (!shapeIndexBySig.TryGetValue(sig, out var idx))
            {
                idx = canonKeys.Count;
                shapeIndexBySig[sig] = idx;
                canonKeys.Add(canon);
                representatives.Add(node.Representative);
            }
            else if (PreferPlacement(node.Representative, representatives[idx]))
            {
                representatives[idx] = node.Representative;
            }
            return idx;
        }

        var nodeShape = new int[nodes.Count];
        for (var i = 0; i < nodes.Count; i++) nodeShape[i] = ShapeOf(nodes[i]);

        var byNecklace = new Dictionary<string, ShapeEdge>();
        foreach (var e in edges)
        {
            var from = nodes[e.FromIndex];
            var to = nodes[e.ToIndex];
            var necklace = PairNecklace(from.Keys, to.Keys, ktet);
            var candidate = new ShapeEdge(
                nodeShape[e.FromIndex], nodeShape[e.ToIndex],
                from.Representative, to.Representative, e.DistinguishingKeys);
            if (!byNecklace.TryGetValue(necklace, out var existing) || PreferEdge(candidate, existing))
                byNecklace[necklace] = candidate;
        }

        var shapes = canonKeys
            .Select((keys, i) => new ShapeNode(keys, representatives[i]))
            .ToList();
        var shapeEdges = byNecklace.Values
            .OrderBy(e => e.FromShapeIndex)
            .ThenBy(e => e.ToShapeIndex)
            .ThenBy(e => e.From.Lcm)
            .ThenBy(e => e.To.Lcm)
            .ToList();
        return (shapes, shapeEdges);
    }

    // Lexicographically smallest (fromKeys, toKeys) pair over all simultaneous transpositions — the
    // necklace that identifies a cover edge up to transposition.
    private static string PairNecklace(IReadOnlyList<int> fromKeys, IReadOnlyList<int> toKeys, int ktet)
    {
        string? best = null;
        for (var t = 0; t < ktet; t++)
        {
            var shifted = Signature(fromKeys.Select(k => Mod(k + t, ktet)))
                + "|" + Signature(toKeys.Select(k => Mod(k + t, ktet)));
            if (best is null || string.CompareOrdinal(shifted, best) < 0) best = shifted;
        }
        return best!;
    }

    private static bool PreferPlacement(Placement a, Placement b) =>
        PlacementKey(a).CompareTo(PlacementKey(b)) < 0;

    private static (int, int, int) PlacementKey(Placement p) =>
        (IsPowerOf2(p.Lcm) ? 0 : 1, p.Lcm, p.At);

    // Prefer an edge rooted at anchor 0, then a power-of-2 / lowest source LCM, then lowest target.
    private static bool PreferEdge(ShapeEdge a, ShapeEdge b) =>
        EdgeKey(a).CompareTo(EdgeKey(b)) < 0;

    private static (int, int, int, int, int) EdgeKey(ShapeEdge e) =>
        (e.From.At, IsPowerOf2(e.From.Lcm) ? 0 : 1, e.From.Lcm, e.To.Lcm, e.To.At);

    private static bool StrictSubset(HashSet<int> a, HashSet<int> b) =>
        a.Count < b.Count && a.IsSubsetOf(b);

    private static int Mod(int x, int m) => (x % m + m) % m;

    private static bool IsPowerOf2(int n) => n > 0 && (n & (n - 1)) == 0;

    private static string Signature(IEnumerable<int> keys) =>
        string.Join(",", keys.Distinct().OrderBy(k => k));
}
