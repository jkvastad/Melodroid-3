namespace Melodroid_3.Music;

// One scale row of `table scale-fingerprint`: a playable key set that contains the chord, derived
// from first principles rather than a curated catalog. A row is either a single LCM-family placement
// (S/PB null, PA alone) or a composite scale — the stable union of a bridged placement pair PA, PB
// sharing an LCM-family subset S (|S| ≥ minSubsetNotes), exactly as `table composite-scales` builds
// them. Keys is the sorted, distinct 12-tet key set to play.
public readonly record struct FingerprintRow(
    IReadOnlyList<int> Keys, Placement PA, Placement? S, Placement? PB);

// Builds the scale rows for `table scale-fingerprint` from the subset principle: every single
// LCM-family placement and every composite scale that contains the chord, parameterised by the
// bridge floor minSubsetNotes. Replaces the old fixed six-scale catalog (NamedScales.ContainingChord)
// — names are now an optional *display* relabelling (NamedScales.Match), not the row source.
public static class ScaleFingerprint
{
    // One row per distinct playable key set containing chordKeys:
    //   - single placements via Placements.FindSupersets (captures e.g. major = 24@0),
    //   - composites via CompositeScales.Enumerate (stable union of a bridged pair, |S| ≥ minSubsetNotes).
    // Rows sharing a key set collapse to one representative (single placement preferred over composite,
    // then lowest-LCM constituents). Sorted by key count, then keys ascending, for stable display.
    // Raising minSubsetNotes only drops composite rows; single-placement rows are unaffected.
    public static IReadOnlyList<FingerprintRow> ContainingChord(
        IReadOnlyCollection<int> chordKeys,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        int ktet)
    {
        var chord = new HashSet<int>(chordKeys.Select(key => ((key % ktet) + ktet) % ktet));

        var candidates = new List<FingerprintRow>();

        // Single LCM-family placements containing the chord.
        foreach (var row in Placements.FindSupersets(chord, families, ktet))
        {
            var keys = row.Placement.Keys.Distinct().OrderBy(k => k).ToList();
            candidates.Add(new FingerprintRow(keys, row.Placement, null, null));
        }

        // Composite scales containing the chord. dedupeTransposition:false keeps every anchor whose
        // union contains the chord; stable:true so a composite's keys match the named necklaces that
        // --name-composites relabels against.
        var composites = CompositeScales.Enumerate(
            families, minSubsetNotes, stable: true, collapseIsomorphic, poolMaximal,
            dedupeTransposition: false, ktet);
        foreach (var c in composites)
        {
            if (chord.IsSubsetOf(c.Keys))
                candidates.Add(new FingerprintRow(c.Keys, c.PA, c.S, c.PB));
        }

        // Collapse rows with identical key sets (the matrix is membership-only) to one representative.
        var best = new Dictionary<string, FingerprintRow>();
        foreach (var row in candidates)
        {
            var sig = string.Join(",", row.Keys);
            if (!best.TryGetValue(sig, out var current) || RepRank(row).CompareTo(RepRank(current)) < 0)
                best[sig] = row;
        }

        return best.Values
            .OrderBy(r => r.Keys.Count)
            .ThenBy(r => string.Join(",", r.Keys.Select(k => k.ToString("D2"))))
            .ToList();
    }

    // Representative rank within one key set: prefer a single placement (IsComposite 0 < 1), then the
    // lowest-LCM / lowest-anchor constituents, then the largest bridge. Mirrors CompositeScales.RepRank.
    private static (int, int, int, int, int, int) RepRank(FingerprintRow r)
    {
        var isComposite = r.PB is null ? 0 : 1;
        var pbLcm = r.PB?.Lcm ?? 0;
        var pbAt = r.PB?.At ?? 0;
        var sCount = r.S?.Keys.Count ?? 0;
        return (isComposite, r.PA.Lcm, r.PA.At, pbLcm, pbAt, -sCount);
    }
}
