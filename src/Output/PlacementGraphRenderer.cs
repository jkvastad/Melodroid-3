using System.Text;
using Melodroid_3.Music;

namespace Melodroid_3.Output;

public static class PlacementGraphRenderer
{
    // A single placement graph (root + direct successors) or an extended placement graph (root's full
    // up-set): both are a scoped view of the universe, rendering the given node indices and every cover
    // edge with both endpoints in scope.
    public static string RenderScoped(
        string title,
        IReadOnlyList<PlacementNode> nodes,
        IReadOnlyList<CoverEdge> edges,
        IReadOnlyCollection<int> scope,
        int maxSize, int maxPrime, int maxLcm, int ktet,
        bool showIsomorphisms = false)
    {
        var inScope = new HashSet<int>(scope);
        var inScopeEdges = edges
            .Where(e => inScope.Contains(e.FromIndex) && inScope.Contains(e.ToIndex))
            .ToList();

        // Branch-group the declaration order (DFS pre-order from the root) so dagre's layout starts
        // near-planar and crosses fewer wires. scope[0] is the minimal/root node in both modes.
        var order = PlacementGraph.SeedOrder(
            scope, inScopeEdges.Select(e => (e.FromIndex, e.ToIndex)), scope.First());
        var position = new Dictionary<int, int>();
        for (var p = 0; p < order.Count; p++) position[order[p]] = p;

        var sb = new StringBuilder();
        PlacementHeader(sb, title, maxSize, maxPrime, maxLcm, ktet, showIsomorphisms);

        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart TD");
        foreach (var i in order) sb.AppendLine($"    {NodeDef(i, nodes[i], showIsomorphisms)}");
        sb.AppendLine();
        foreach (var e in inScopeEdges
            .OrderBy(e => position[e.FromIndex])
            .ThenBy(e => position[e.ToIndex]))
        {
            sb.AppendLine($"    P{e.FromIndex} -- \"+{JoinCommas(e.DistinguishingKeys)}\" --> P{e.ToIndex}");
        }
        sb.AppendLine("```");
        return sb.ToString();
    }

    public static string RenderShapeGraph(
        IReadOnlyList<ShapeNode> shapes,
        IReadOnlyList<ShapeEdge> edges,
        int maxSize, int maxPrime, int maxLcm, int ktet)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Placement Shape Graph");
        sb.AppendLine();
        Settings(sb, maxSize, maxPrime, maxLcm, ktet);
        sb.AppendLine("**Legend**");
        sb.AppendLine();
        sb.AppendLine("- Node — one shape (transposition class of placements), labelled by note count, an example placement `L@a`, and the normal-order key set.");
        sb.AppendLine("- Arrow `L@a→L'@b (+keys)` — one direct-adjacency relationship between shapes, shown with a representative concrete placement pair and its distinguishing keys. Every other placement of that shape realises the same relationship transposed.");
        sb.AppendLine();

        // Branch-group the declaration order (DFS pre-order from the 1-note tonic shape 0, subset of all)
        // so dagre seeds near-planar and crosses fewer wires.
        var order = PlacementGraph.SeedOrder(
            Enumerable.Range(0, shapes.Count).ToList(),
            edges.Select(e => (e.FromShapeIndex, e.ToShapeIndex)), 0);
        var position = new Dictionary<int, int>();
        for (var p = 0; p < order.Count; p++) position[order[p]] = p;

        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart TD");
        foreach (var i in order)
        {
            var count = shapes[i].CanonicalKeys.Count;
            sb.AppendLine($"    S{i}[\"{count}-note · e.g. {Label(shapes[i].Representative)}<br/>{{{Join(shapes[i].CanonicalKeys)}}}\"]");
        }
        sb.AppendLine();
        foreach (var e in edges
            .OrderBy(e => position[e.FromShapeIndex])
            .ThenBy(e => position[e.ToShapeIndex]))
        {
            sb.AppendLine($"    S{e.FromShapeIndex} -- \"{Label(e.From)}→{Label(e.To)} (+{JoinCommas(e.DistinguishingKeys)})\" --> S{e.ToShapeIndex}");
        }
        sb.AppendLine("```");
        return sb.ToString();
    }

    private static void PlacementHeader(
        StringBuilder sb, string title, int maxSize, int maxPrime, int maxLcm, int ktet,
        bool showIsomorphisms)
    {
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        Settings(sb, maxSize, maxPrime, maxLcm, ktet);
        sb.AppendLine("**Legend**");
        sb.AppendLine();
        sb.AppendLine(showIsomorphisms
            ? "- Node `L@a` — placement of LCM family `L` anchored at key `a`, with its k-tet key set. `≅` lists keyboard-coincident (isomorphic) placements with identical keys."
            : "- Node `L@a` — a representative placement of LCM family `L` anchored at key `a`, with its k-tet key set. Keyboard-coincident (isomorphic) placements are folded in; pass `--show-isomorphisms` to list them with `≅`.");
        sb.AppendLine("- Arrow `--> +keys` — direct adjacency (cover): the target contains the source, adding exactly the labelled distinguishing keys, with no placement strictly between.");
        sb.AppendLine();
    }

    private static void Settings(StringBuilder sb, int maxSize, int maxPrime, int maxLcm, int ktet)
    {
        sb.AppendLine($"Good fractions `--max-size {maxSize}`, `--max-prime {maxPrime}`; families up to `--max-lcm {maxLcm}`; `--ktet {ktet}`.");
        sb.AppendLine();
    }

    private static string NodeDef(int index, PlacementNode node, bool showIsomorphisms)
    {
        var label = Label(node.Representative);
        if (showIsomorphisms && node.Aliases.Count > 0)
            label += " ≅ " + string.Join(" ≅ ", node.Aliases.Select(Label));
        return $"P{index}[\"{label}<br/>{{{Join(node.Keys)}}}\"]";
    }

    private static string Label(Placement p) => $"{p.Lcm}@{p.At}";

    private static string Join(IReadOnlyList<int> keys) => string.Join(" ", keys);

    private static string JoinCommas(IReadOnlyList<int> keys) => string.Join(",", keys);
}
