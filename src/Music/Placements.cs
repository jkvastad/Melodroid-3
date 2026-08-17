namespace Melodroid_3.Music;

public readonly record struct Placement(int Lcm, int At, IReadOnlyList<int> Keys);

public readonly record struct FamilyOverlapRow(int At, IReadOnlyList<int> AKeys, IReadOnlyList<int> Intersection);

public readonly record struct KeySupersetRow(Placement Placement, IReadOnlyList<Fraction> Fractions, int ExtraKeysCount);

public static class Placements
{
    public static Placement Compute(LcmFamily family, int at, int ktet)
    {
        var keys = new List<int>(family.Fractions.Count);
        foreach (var f in family.Fractions)
        {
            var k0 = KeysNeeded.NearestKey(f.Value, ktet).N;
            keys.Add(((k0 + at) % ktet + ktet) % ktet);
        }
        return new Placement(family.Lcm, at, keys);
    }

    public static IReadOnlyList<Placement> Sweep(LcmFamily family, int ktet)
    {
        var result = new List<Placement>(ktet);
        for (var at = 0; at < ktet; at++)
        {
            result.Add(Compute(family, at, ktet));
        }
        return result;
    }

    public static (IReadOnlyList<int> BKeysAtZero, IReadOnlyList<FamilyOverlapRow> Rows) OverlapSweep(
        LcmFamily a, LcmFamily b, int ktet)
    {
        var bKeys = Compute(b, 0, ktet).Keys;
        var bSet = new HashSet<int>(bKeys);

        var rows = new List<FamilyOverlapRow>(ktet);
        for (var at = 0; at < ktet; at++)
        {
            var placement = Compute(a, at, ktet);
            var intersection = placement.Keys
                .Where(bSet.Contains)
                .Distinct()
                .OrderBy(k => k)
                .ToList();
            rows.Add(new FamilyOverlapRow(at, placement.Keys, intersection));
        }

        var bKeysSorted = bKeys.Distinct().OrderBy(k => k).ToList();
        return (bKeysSorted, rows);
    }

    public static IReadOnlyList<KeySupersetRow> FindSupersets(
        IReadOnlyCollection<int> requestedKeys,
        IReadOnlyList<LcmFamily> families,
        int ktet)
    {
        var requested = new HashSet<int>(requestedKeys);
        var rows = new List<KeySupersetRow>();

        foreach (var family in families)
        {
            for (var at = 0; at < ktet; at++)
            {
                var placement = Compute(family, at, ktet);
                var placementKeySet = new HashSet<int>(placement.Keys);
                if (!requested.IsSubsetOf(placementKeySet)) continue;

                var extra = placementKeySet.Count - requested.Count;
                rows.Add(new KeySupersetRow(placement, family.Fractions, extra));
            }
        }

        return rows
            .OrderBy(r => r.ExtraKeysCount)
            .ThenBy(r => r.Placement.Lcm)
            .ThenBy(r => r.Placement.At)
            .ToList();
    }

    // Partition two superset result lists (from FindSupersets) by placement identity (Lcm, At):
    // Common = placements that are a superset of both key sets; OnlyA / OnlyB = placements unique
    // to one. Incoming sort order (extra → lcm → at) is preserved. Used by `key-supersets --compare`.
    public static (
        IReadOnlyList<KeySupersetRow> Common,
        IReadOnlyList<KeySupersetRow> OnlyA,
        IReadOnlyList<KeySupersetRow> OnlyB) CompareSupersets(
            IReadOnlyList<KeySupersetRow> supersetsA,
            IReadOnlyList<KeySupersetRow> supersetsB)
    {
        static (int, int) Id(KeySupersetRow r) => (r.Placement.Lcm, r.Placement.At);

        var idsA = new HashSet<(int, int)>(supersetsA.Select(Id));
        var idsB = new HashSet<(int, int)>(supersetsB.Select(Id));

        var common = supersetsA.Where(r => idsB.Contains(Id(r))).ToList();
        var onlyA = supersetsA.Where(r => !idsB.Contains(Id(r))).ToList();
        var onlyB = supersetsB.Where(r => !idsA.Contains(Id(r))).ToList();

        return (common, onlyA, onlyB);
    }

    // Collapse placements that cover the identical key set (isomorphic / coincident) down
    // to the single lowest-LCM representative. Assumes `rows` is already sorted by
    // (extra, lcm, at) as FindSupersets returns, so the first row seen per key set is the
    // lowest LCM. Used by `table chords`; FindSupersets itself stays exhaustive.
    public static IReadOnlyList<KeySupersetRow> CollapseIsomorphic(IReadOnlyList<KeySupersetRow> rows)
    {
        var seen = new HashSet<string>();
        var result = new List<KeySupersetRow>();
        foreach (var r in rows)
        {
            var signature = string.Join(",", r.Placement.Keys.OrderBy(k => k));
            if (seen.Add(signature)) result.Add(r);
        }
        return result;
    }

    public static IReadOnlyList<int> MaximalLcms(
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<FamilyRelation> relations,
        bool dropRenormalizedSubsets = false)
    {
        // Literal subsets always count as domination. Renormalized subsets are kept as their own
        // rows by default (their keys are merely subsumed by a larger family's placement), but
        // dropRenormalizedSubsets folds them into domination too — the stricter, pre-collapse view.
        // (RenormalizedSubset relations are always produced by FamilyRelations and used by the
        // graph renderer regardless of this flag.)
        var dominated = new HashSet<int>(relations
            .Where(r => r.Kind is RelationKind.LiteralSubset
                || (dropRenormalizedSubsets && r.Kind is RelationKind.RenormalizedSubset))
            .Select(r => r.FromLcm));

        var candidates = families
            .Select(f => f.Lcm)
            .Where(lcm => !dominated.Contains(lcm))
            .ToList();

        // Collapse each isomorphism class to its lowest-LCM representative: isomorphic families are
        // renormalizations (keyboard transpositions) of one another, so their placements coincide
        // across the anchor sweep and only the lowest LCM is kept as the canonical row.
        var parent = new Dictionary<int, int>();
        int Find(int x)
        {
            if (!parent.TryGetValue(x, out _)) parent[x] = x;
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }
        foreach (var r in relations.Where(r => r.Kind is RelationKind.Isomorphism))
        {
            parent[Find(r.FromLcm)] = Find(r.ToLcm);
        }

        var lowestByRoot = new Dictionary<int, int>();
        foreach (var lcm in candidates)
        {
            var root = Find(lcm);
            if (!lowestByRoot.TryGetValue(root, out var cur) || lcm < cur) lowestByRoot[root] = lcm;
        }
        var representatives = new HashSet<int>(lowestByRoot.Values);

        return candidates
            .Where(representatives.Contains)
            .OrderBy(lcm => lcm)
            .ToList();
    }

    // The collapsing key of an lcm-15 placement: playing key (At+8) mod 12 reinterprets 15@At as the
    // adjacent lcm-24 placements 24@(At+1)/24@(At+8) ("lcm 24 preference"). 12-tet / lcm-15 only,
    // matching the adjacency rule in ChordProgressions. Null for any other family or ktet.
    public static int? CollapsingKey(Placement placement, int ktet) =>
        ktet == 12 && placement.Lcm == 15 ? (placement.At + 8) % 12 : null;

    // Drop lcm-15 placements whose collapsing key is one of the chord keys (they have collapsed onto
    // an lcm-24 under lcm-24 preference). Other placements pass through untouched.
    public static IReadOnlyList<Placement> DropCollapsed(
        IReadOnlyList<Placement> placements,
        IReadOnlyCollection<int> chordKeys,
        int ktet)
    {
        var chord = new HashSet<int>(chordKeys);
        return placements
            .Where(p => CollapsingKey(p, ktet) is not int ck || !chord.Contains(ck))
            .ToList();
    }

    // Reduce each lcm-15 placement to its stable form (15s@At) by dropping its collapsing key
    // (At+8) mod 12 — the key whose lcm-24 preference collapses 15@At onto an adjacent lcm-24. The
    // remaining keys are exactly the stable 15s subset (e.g. 15@0 → 0 1 3 5 8 9 10 becomes
    // 0 1 3 5 9 10). Other placements pass through untouched. 12-tet / lcm-15 only (CollapsingKey is
    // null otherwise). A stabilized row is recognisable by the absence of its collapsing key, which
    // a full 15@At placement always contains.
    public static IReadOnlyList<Placement> StableFifteen(
        IReadOnlyList<Placement> placements,
        int ktet)
    {
        var result = new List<Placement>(placements.Count);
        foreach (var p in placements)
        {
            if (CollapsingKey(p, ktet) is int ck)
                result.Add(p with { Keys = p.Keys.Where(key => key != ck).ToList() });
            else
                result.Add(p);
        }
        return result;
    }

    public static IReadOnlyList<Placement> FindMaximalContaining(
        IReadOnlyCollection<int> chordKeys,
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<FamilyRelation> relations,
        int ktet,
        bool dropRenormalizedSubsets = false)
    {
        var maximalSet = new HashSet<int>(MaximalLcms(families, relations, dropRenormalizedSubsets));
        var chord = new HashSet<int>(chordKeys);
        var rows = new List<Placement>();

        foreach (var family in families)
        {
            if (!maximalSet.Contains(family.Lcm)) continue;
            for (var at = 0; at < ktet; at++)
            {
                var placement = Compute(family, at, ktet);
                if (!chord.IsSubsetOf(placement.Keys)) continue;
                rows.Add(placement);
            }
        }

        return rows
            .OrderBy(p => p.Lcm)
            .ThenBy(p => p.At)
            .ToList();
    }
}
