namespace Melodroid_3.Music;

public enum RelationKind
{
    LiteralSubset,
    Isomorphism,
    RenormalizedSubset,
}

// Base is the fraction b in the LCM family F_FromLcm with LCM FromLcm used for renormalization: ren(F_FromLcm, b) can equal F_ToLcm (Isomorphism) or be a proper subset of F_ToLcm (RenormalizedSubset). Null for LiteralSubset (no renormalization needed).
public readonly record struct FamilyRelation(
    int FromLcm,
    int ToLcm,
    RelationKind Kind,
    Fraction? Base);

public static class FamilyRelations
{
    public static IReadOnlyList<FamilyRelation> Compute(IReadOnlyList<LcmFamily> families)
    {
        var literal = new List<(int From, int To)>();
        var iso = new Dictionary<(int From, int To), Fraction>();
        var renSubset = new Dictionary<(int From, int To), Fraction>();

        for (var i = 0; i < families.Count; i++)
        {
            for (var j = 0; j < families.Count; j++)
            {
                if (i == j) continue;
                var a = families[i];
                var b = families[j];

                if (a.Fractions.Count < b.Fractions.Count && IsSubset(a.Fractions, b.Fractions))
                {
                    literal.Add((a.Lcm, b.Lcm));
                    continue;
                }

                if (a.Fractions.Count == b.Fractions.Count && a.Lcm < b.Lcm)
                {
                    var baseFrac = FindIsoBase(a.Fractions, b.Fractions);
                    if (baseFrac is not null)
                    {
                        iso[(a.Lcm, b.Lcm)] = baseFrac.Value;
                    }
                    continue;
                }

                if (a.Fractions.Count < b.Fractions.Count)
                {
                    var baseFrac = FindRenSubsetBase(a.Fractions, b.Fractions);
                    if (baseFrac is not null)
                    {
                        renSubset[(a.Lcm, b.Lcm)] = baseFrac.Value;
                    }
                }
            }
        }

        var literalReduced = HasseReduce(literal);
        var renSubsetReduced = HasseReduce(new List<(int From, int To)>(renSubset.Keys));
        var isoReduced = ReduceIsoEdges(iso);

        var result = new List<FamilyRelation>();
        foreach (var (from, to) in literalReduced)
        {
            result.Add(new FamilyRelation(from, to, RelationKind.LiteralSubset, null));
        }
        foreach (var (from, to) in isoReduced)
        {
            result.Add(new FamilyRelation(from, to, RelationKind.Isomorphism, iso[(from, to)]));
        }
        foreach (var (from, to) in renSubsetReduced)
        {
            result.Add(new FamilyRelation(from, to, RelationKind.RenormalizedSubset, renSubset[(from, to)]));
        }
        return result;
    }

    private static bool IsSubset(IReadOnlyList<Fraction> a, IReadOnlyList<Fraction> b)
    {
        var bSet = new HashSet<Fraction>(b);
        foreach (var f in a) if (!bSet.Contains(f)) return false;
        return true;
    }

    private static Fraction? FindIsoBase(IReadOnlyList<Fraction> a, IReadOnlyList<Fraction> b)
    {
        var bSet = new HashSet<Fraction>(b);
        foreach (var baseFrac in a)
        {
            var ren = Renormalization.Renormalize(a, baseFrac);
            if (ren.Count != b.Count) continue;
            if (ren.All(bSet.Contains)) return baseFrac;
        }
        return null;
    }

    private static Fraction? FindRenSubsetBase(IReadOnlyList<Fraction> a, IReadOnlyList<Fraction> b)
    {
        var bSet = new HashSet<Fraction>(b);
        var unity = new Fraction(1, 1);
        foreach (var baseFrac in a)
        {
            if (baseFrac == unity) continue;
            var ren = Renormalization.Renormalize(a, baseFrac);
            if (ren.All(bSet.Contains)) return baseFrac;
        }
        return null;
    }

    public static IReadOnlyList<IReadOnlyList<int>> BuildIsoClasses(
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

    private static List<(int From, int To)> HasseReduce(List<(int From, int To)> edges)
    {
        var adj = new Dictionary<int, HashSet<int>>();
        foreach (var (from, to) in edges)
        {
            if (!adj.TryGetValue(from, out var set))
            {
                set = new HashSet<int>();
                adj[from] = set;
            }
            set.Add(to);
        }

        var result = new List<(int From, int To)>();
        foreach (var (from, to) in edges)
        {
            if (!HasIntermediate(from, to, adj))
            {
                result.Add((from, to));
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

    private static List<(int From, int To)> ReduceIsoEdges(Dictionary<(int From, int To), Fraction> isoEdges)
    {
        var nodes = new HashSet<int>();
        foreach (var (from, to) in isoEdges.Keys)
        {
            nodes.Add(from);
            nodes.Add(to);
        }

        var parent = new Dictionary<int, int>();
        foreach (var n in nodes) parent[n] = n;

        int Find(int x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }

        foreach (var (from, to) in isoEdges.Keys)
        {
            parent[Find(from)] = Find(to);
        }

        var classes = nodes.GroupBy(Find).Select(g => g.OrderBy(x => x).ToList());
        var result = new List<(int From, int To)>();
        foreach (var cls in classes)
        {
            for (var i = 0; i < cls.Count - 1; i++)
            {
                var edge = (cls[i], cls[i + 1]);
                if (isoEdges.ContainsKey(edge)) result.Add(edge);
            }
        }
        return result;
    }
}
