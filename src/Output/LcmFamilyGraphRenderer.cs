using System.Text;
using Melodroid_3.Music;

namespace Melodroid_3.Output;

public static class LcmFamilyGraphRenderer
{
    public static string Render(
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<FamilyRelation> relations,
        int maxSize,
        int maxPrime,
        int maxLcm)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# LCM Family Relationships");
        sb.AppendLine();
        sb.AppendLine($"Good fractions enumerated with `--max-size {maxSize}`, `--max-prime {maxPrime}`. Families computed up to `--max-lcm {maxLcm}`.");
        sb.AppendLine();
        sb.AppendLine("**Legend**");
        sb.AppendLine();
        sb.AppendLine("- Solid arrow `-->` — literal subset: `F_A ⊆ F_B` as sets.");
        sb.AppendLine("- Thick double-headed arrow `<==>` — isomorphism: a base `b ∈ F_A` makes `ren(F_A, b) = F_B` exactly. Edge label gives the base.");
        sb.AppendLine("- Dashed arrow `-.->` — renormalized (proper) subset: a base `b` makes `ren(F_A, b) ⊂ F_B`. Edge label gives the base.");
        sb.AppendLine();
        sb.AppendLine("Edges are Hasse-reduced per relation (transitive edges of the same kind are omitted). Isomorphic families are grouped into subgraphs.");
        sb.AppendLine();

        var isoEdges = relations.Where(r => r.Kind == RelationKind.Isomorphism).ToList();
        var classes = BuildIsoClasses(families, relations);

        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart TD");

        var emitted = new HashSet<int>();
        var classIdx = 0;
        foreach (var cls in classes.Where(c => c.Count >= 2))
        {
            classIdx++;
            sb.AppendLine($"    subgraph iso_{classIdx} [\"≅ class {classIdx}\"]");
            sb.AppendLine("        direction LR");
            foreach (var lcm in cls)
            {
                var family = families.First(f => f.Lcm == lcm);
                sb.AppendLine($"        {NodeDef(family)}");
                emitted.Add(lcm);
            }
            sb.AppendLine("    end");
        }
        foreach (var family in families)
        {
            if (emitted.Contains(family.Lcm)) continue;
            sb.AppendLine($"    {NodeDef(family)}");
        }
        sb.AppendLine();

        foreach (var r in isoEdges)
        {
            sb.AppendLine($"    L{r.FromLcm} <== \"b={r.Base}\" ==> L{r.ToLcm}");
        }
        foreach (var r in relations.Where(r => r.Kind == RelationKind.LiteralSubset))
        {
            sb.AppendLine($"    L{r.FromLcm} --> L{r.ToLcm}");
        }
        foreach (var r in relations.Where(r => r.Kind == RelationKind.RenormalizedSubset))
        {
            sb.AppendLine($"    L{r.FromLcm} -. \"b={r.Base}\" .-> L{r.ToLcm}");
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    public static string RenderCollapsed(
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<FamilyRelation> relations,
        int maxSize,
        int maxPrime,
        int maxLcm)
    {
        var classes = BuildIsoClasses(families, relations);
        var lcmToClass = new Dictionary<int, int>();
        for (var i = 0; i < classes.Count; i++)
        {
            foreach (var lcm in classes[i]) lcmToClass[lcm] = i + 1;
        }

        var literalClassEdges = CollapseAndReduce(
            relations.Where(r => r.Kind == RelationKind.LiteralSubset).Select(r => (r.FromLcm, r.ToLcm)),
            lcmToClass);
        var renSubsetClassEdges = CollapseAndReduce(
            relations.Where(r => r.Kind == RelationKind.RenormalizedSubset).Select(r => (r.FromLcm, r.ToLcm)),
            lcmToClass);

        var sb = new StringBuilder();
        sb.AppendLine("# LCM Family Relationships — Collapsed by Isomorphism Class");
        sb.AppendLine();
        sb.AppendLine($"Good fractions enumerated with `--max-size {maxSize}`, `--max-prime {maxPrime}`. Families computed up to `--max-lcm {maxLcm}`.");
        sb.AppendLine();
        sb.AppendLine("Each node is one isomorphism class (singletons included). Intra-class isomorphism edges are omitted by construction.");
        sb.AppendLine();
        sb.AppendLine("**Legend**");
        sb.AppendLine();
        sb.AppendLine("- Solid arrow `-->` — literal subset: some family in class A is a literal subset of some family in class B.");
        sb.AppendLine("- Dashed arrow `-.->` — renormalized subset: some family in class A renormalizes into a proper subset of some family in class B.");
        sb.AppendLine();
        sb.AppendLine("Class-to-class edges are deduplicated and Hasse-reduced per kind (transitive edges of the same kind are omitted).");
        sb.AppendLine();

        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart TD");

        for (var i = 0; i < classes.Count; i++)
        {
            var idx = i + 1;
            var lcms = classes[i];
            var members = $"LCM={lcms[0]}" + string.Concat(lcms.Skip(1).Select(l => $", {l}"));
            sb.AppendLine($"    C{idx}[\"class {idx}<br/>{{{members}}}\"]");
        }
        sb.AppendLine();

        foreach (var (from, to) in literalClassEdges.OrderBy(e => e.From).ThenBy(e => e.To))
        {
            sb.AppendLine($"    C{from} --> C{to}");
        }
        foreach (var (from, to) in renSubsetClassEdges.OrderBy(e => e.From).ThenBy(e => e.To))
        {
            sb.AppendLine($"    C{from} -.-> C{to}");
        }

        sb.AppendLine("```");
        return sb.ToString();
    }

    public static IReadOnlyCollection<(int From, int To)> CollapseAndReduce(
        IEnumerable<(int FromLcm, int ToLcm)> familyEdges,
        IReadOnlyDictionary<int, int> lcmToClass)
    {
        var classEdges = new HashSet<(int From, int To)>();
        foreach (var (fromLcm, toLcm) in familyEdges)
        {
            var from = lcmToClass[fromLcm];
            var to = lcmToClass[toLcm];
            if (from == to) continue;
            classEdges.Add((from, to));
        }

        var adj = new Dictionary<int, HashSet<int>>();
        foreach (var (from, to) in classEdges)
        {
            if (!adj.TryGetValue(from, out var set))
            {
                set = new HashSet<int>();
                adj[from] = set;
            }
            set.Add(to);
        }

        var result = new List<(int From, int To)>();
        foreach (var edge in classEdges)
        {
            if (!HasIntermediate(edge.From, edge.To, adj))
            {
                result.Add(edge);
            }
        }
        return result;
    }

    private static bool HasIntermediate(int from, int to, Dictionary<int, HashSet<int>> adj)
    {
        if (!adj.TryGetValue(from, out var neighbors)) return false;
        var visited = new HashSet<int> { from };
        var queue = new Queue<int>();
        foreach (var n in neighbors)
        {
            if (n == to) continue;
            if (visited.Add(n)) queue.Enqueue(n);
        }
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == to) return true;
            if (adj.TryGetValue(cur, out var nbrs))
            {
                foreach (var n in nbrs)
                {
                    if (visited.Add(n)) queue.Enqueue(n);
                }
            }
        }
        return false;
    }

    private static string NodeDef(LcmFamily family)
    {
        var fractions = string.Join(", ", family.Fractions.Select(f => f.ToString()));
        return $"L{family.Lcm}[\"LCM={family.Lcm}<br/>{{{fractions}}}\"]";
    }

    private static IReadOnlyList<IReadOnlyList<int>> BuildIsoClasses(
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<FamilyRelation> relations)
    {
        var parent = new Dictionary<int, int>();
        foreach (var f in families) parent[f.Lcm] = f.Lcm;

        int Find(int x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }

        foreach (var r in relations)
        {
            if (r.Kind != RelationKind.Isomorphism) continue;
            parent[Find(r.FromLcm)] = Find(r.ToLcm);
        }

        return families
            .Select(f => f.Lcm)
            .GroupBy(Find)
            .Select(g => (IReadOnlyList<int>)g.OrderBy(x => x).ToList())
            .OrderBy(g => g[0])
            .ToList();
    }
}
