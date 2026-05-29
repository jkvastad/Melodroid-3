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

    public static IReadOnlyList<int> MaximalLcms(
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<FamilyRelation> relations)
    {
        var dominated = new HashSet<int>(relations
            .Where(r => r.Kind is RelationKind.LiteralSubset or RelationKind.RenormalizedSubset)
            .Select(r => r.FromLcm));
        return families
            .Select(f => f.Lcm)
            .Where(lcm => !dominated.Contains(lcm))
            .OrderBy(lcm => lcm)
            .ToList();
    }

    public static IReadOnlyList<Placement> FindMaximalContaining(
        IReadOnlyCollection<int> chordKeys,
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<FamilyRelation> relations,
        int ktet)
    {
        var maximalSet = new HashSet<int>(MaximalLcms(families, relations));
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
