using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class PlacementGraphTests
{
    private static IReadOnlyList<LcmFamily> StandardFamilies() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(maxSize: 24, maxPrime: 5), maxLcm: 24);

    private static IReadOnlyList<PlacementNode> Universe() =>
        PlacementGraph.BuildUniverse(StandardFamilies(), ktet: 12);

    // The directly-adjacent supersets of the placement whose key set equals rootKeys, as sorted key sets.
    private static IReadOnlyList<IReadOnlyList<int>> DirectSuccessorKeySets(int[] rootKeys)
    {
        var nodes = Universe();
        var edges = PlacementGraph.CoverEdges(nodes);
        var root = PlacementGraph.FindNode(nodes, rootKeys);
        return edges
            .Where(e => e.FromIndex == root)
            .Select(e => (IReadOnlyList<int>)nodes[e.ToIndex].Keys.OrderBy(k => k).ToList())
            .ToList();
    }

    [Fact]
    public void Tonic_direct_successors_are_the_two_dyads_and_the_triad()
    {
        var successors = DirectSuccessorKeySets(new[] { 0 });

        successors.Should().BeEquivalentTo(new[]
        {
            new[] { 0, 7 },
            new[] { 0, 5 },
            new[] { 0, 3, 8 },
        });
    }

    [Fact]
    public void Dyad_direct_successors_match_the_docs_dyad_placement_graph()
    {
        var successors = DirectSuccessorKeySets(new[] { 0, 7 });

        successors.Should().BeEquivalentTo(new[]
        {
            new[] { 0, 4, 7 },             // 4@0 (triad)
            new[] { 0, 5, 7, 9 },          // 5@9 (tetrad)
            new[] { 0, 3, 7, 8, 10 },      // 8@8 (pentad)
            new[] { 0, 2, 3, 5, 7, 10 },   // 18@5 (hexad)
        });
    }

    [Fact]
    public void BuildUniverse_folds_coincident_key_sets_preferring_a_power_of_two_representative()
    {
        var nodes = Universe();

        var triad038 = nodes.Single(n => n.Keys.OrderBy(k => k).SequenceEqual(new[] { 0, 3, 8 }));

        // 3@3 and 4@8 share key set {0,3,8}; the power-of-2 LCM (4) is the representative, 3@3 an alias.
        triad038.Representative.Lcm.Should().Be(4);
        triad038.Representative.At.Should().Be(8);
        triad038.Aliases.Should().ContainSingle()
            .Which.Lcm.Should().Be(3);
    }

    [Fact]
    public void ShapeGraph_has_nine_shapes_and_eighteen_relationships()
    {
        var nodes = Universe();
        var edges = PlacementGraph.CoverEdges(nodes);

        var (shapes, shapeEdges) = PlacementGraph.ShapeGraph(nodes, edges, ktet: 12);

        shapes.Should().HaveCount(9);
        shapeEdges.Should().HaveCount(18);

        // The nine shapes are one each of sizes 1..5 plus two hexads and two heptads.
        shapes.Select(s => s.CanonicalKeys.Count).OrderBy(c => c)
            .Should().Equal(1, 2, 3, 4, 5, 6, 6, 7, 7);
    }

    [Fact]
    public void ExtendedTriad_upset_is_the_fifteen_placements_all_containing_the_triad()
    {
        var nodes = Universe();
        var triad = PlacementGraph.FindNode(nodes, new[] { 0, 4, 7 });

        var upSet = PlacementGraph.UpSet(nodes, nodes[triad].Keys);

        upSet.Should().HaveCount(15);
        upSet.Should().Contain(triad);
        foreach (var i in upSet)
            nodes[i].Keys.Should().Contain(new[] { 0, 4, 7 });
    }

    [Fact]
    public void ExtendedTriad_includes_hexad_to_heptad_covers_the_hand_drawn_graph_omitted()
    {
        var nodes = Universe();
        var edges = PlacementGraph.CoverEdges(nodes);
        var triadKeys = nodes[PlacementGraph.FindNode(nodes, new[] { 0, 4, 7 })].Keys;
        var scope = new HashSet<int>(PlacementGraph.UpSet(nodes, triadKeys));

        // A hexad ⊂ heptad is always a cover (nothing fits strictly between a 6- and 7-key set),
        // e.g. 18@7 {0,2,4,5,7,9} + 11 = 24@0 {0,2,4,5,7,9,11}.
        var from = PlacementGraph.FindNode(nodes, new[] { 0, 2, 4, 5, 7, 9 });
        var to = PlacementGraph.FindNode(nodes, new[] { 0, 2, 4, 5, 7, 9, 11 });

        edges.Should().Contain(e =>
            e.FromIndex == from && e.ToIndex == to
            && scope.Contains(e.FromIndex) && scope.Contains(e.ToIndex));
    }

    // The extended-triad scope (up-set of {0,4,7}) plus the cover edges with both endpoints in it, and
    // the root index — the inputs the renderer feeds to SeedOrder.
    private static (IReadOnlyList<int> Scope, List<(int From, int To)> Edges, int Root) ExtendedTriad()
    {
        var nodes = Universe();
        var edges = PlacementGraph.CoverEdges(nodes);
        var root = PlacementGraph.FindNode(nodes, new[] { 0, 4, 7 });
        var scope = PlacementGraph.UpSet(nodes, nodes[root].Keys);
        var inScope = new HashSet<int>(scope);
        var inScopeEdges = edges
            .Where(e => inScope.Contains(e.FromIndex) && inScope.Contains(e.ToIndex))
            .Select(e => (e.FromIndex, e.ToIndex))
            .ToList();
        return (scope, inScopeEdges, root);
    }

    // Every node reachable from start by following the given edges (start excluded unless on a cycle).
    private static HashSet<int> Reachable(int start, IEnumerable<(int From, int To)> edges)
    {
        var adjacency = edges.GroupBy(e => e.From)
            .ToDictionary(g => g.Key, g => g.Select(e => e.To).ToList());
        var seen = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (adjacency.TryGetValue(node, out var successors))
                foreach (var next in successors)
                    if (seen.Add(next)) stack.Push(next);
        }
        return seen;
    }

    [Fact]
    public void SeedOrder_is_a_permutation_of_the_scope()
    {
        var (scope, edges, root) = ExtendedTriad();

        var order = PlacementGraph.SeedOrder(scope, edges, root);

        order.Should().BeEquivalentTo(scope);
    }

    [Fact]
    public void SeedOrder_places_every_non_root_node_after_a_cover_parent()
    {
        var (scope, edges, root) = ExtendedTriad();

        var order = PlacementGraph.SeedOrder(scope, edges, root);
        var position = order.Select((node, i) => (node, i)).ToDictionary(x => x.node, x => x.i);

        foreach (var node in order.Where(n => n != root))
        {
            var parents = edges.Where(e => e.To == node).Select(e => e.From);
            parents.Should().Contain(parent => position[parent] < position[node],
                "a DFS pre-order visits a node only after one of its parents");
        }
    }

    [Fact]
    public void SeedOrder_groups_the_first_branch_contiguously()
    {
        var (scope, edges, root) = ExtendedTriad();

        var order = PlacementGraph.SeedOrder(scope, edges, root);
        order[0].Should().Be(root);

        // The root's first visited child is its lowest-indexed direct successor; its whole subtree must
        // form the contiguous block right after the root, before any other branch begins.
        var firstChild = edges.Where(e => e.From == root).Select(e => e.To).Min();
        var firstBranch = Reachable(firstChild, edges);
        firstBranch.Add(firstChild);

        order.Skip(1).Take(firstBranch.Count).Should().BeEquivalentTo(firstBranch);
    }

    [Fact]
    public void CoverEdges_never_skip_a_placement_that_lies_strictly_between()
    {
        var nodes = Universe();
        var edges = PlacementGraph.CoverEdges(nodes);
        var sets = nodes.Select(n => new HashSet<int>(n.Keys)).ToList();

        foreach (var e in edges)
        {
            for (var m = 0; m < nodes.Count; m++)
            {
                if (m == e.FromIndex || m == e.ToIndex) continue;
                var between = sets[e.FromIndex].IsProperSubsetOf(sets[m])
                    && sets[m].IsProperSubsetOf(sets[e.ToIndex]);
                between.Should().BeFalse("a cover edge admits no node strictly between its endpoints");
            }
        }
    }
}
