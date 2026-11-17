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
    // to one. Incoming sort order (extra → lcm → at) is preserved. Used by `supersets --compare`.
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

    // The collapsing key of an unstable placement: the key whose presence reinterprets it as a
    // smaller/simpler pattern under preference.
    //   - lcm-15: playing (At+8) mod 12 reinterprets 15@At as the adjacent lcm-24 placements
    //     24@(At+1)/24@(At+8) ("lcm 24 preference").
    //   - lcm-20: 20@At is the lcm-20 subset of 15@(At+7) and collapses to it (20@k → 15@(k+7)), so
    //     it shares that parent's collapsing key: ((At+7)+8) mod 12 = (At+3) mod 12.
    // 12-tet only; null for any other family or ktet.
    public static int? CollapsingKey(Placement placement, int ktet) =>
        ktet != 12 ? null
        : placement.Lcm == 15 ? (placement.At + 8) % 12
        : placement.Lcm == 20 ? (placement.At + 3) % 12
        : null;

    // Drop unstable placements (lcm-15 / lcm-20) whose collapsing key is one of the chord keys (they
    // have collapsed onto a smaller pattern under preference). Other placements pass through untouched.
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
    // 0 1 3 5 9 10). Other placements pass through untouched — lcm-15 only (even though CollapsingKey
    // now also fires for lcm-20, the 15s stable form is an lcm-15 concept). A stabilized row is
    // recognisable by the absence of its collapsing key, which a full 15@At placement always contains.
    public static IReadOnlyList<Placement> StableFifteen(
        IReadOnlyList<Placement> placements,
        int ktet)
    {
        var result = new List<Placement>(placements.Count);
        foreach (var p in placements)
        {
            if (p.Lcm == 15 && CollapsingKey(p, ktet) is int ck)
                result.Add(p with { Keys = p.Keys.Where(key => key != ck).ToList() });
            else
                result.Add(p);
        }
        return result;
    }
}
