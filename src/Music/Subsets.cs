namespace Melodroid_3.Music;

public readonly record struct SubsetMatch(
    IReadOnlyList<int> Keys,   // the subset of base keys (sorted ascending)
    int Reference,             // reference key n where the subset full-matches
    IReadOnlyList<int> Lcms,   // candidate LCMs ascending (best fit first); single value when Strict
    bool Strict)               // true = strict full match, false = ambiguous full match
{
    // The best-fit LCM (shortest wave pattern length) — equals KeySweepRow.PostBinLcm.
    public int Lcm => Lcms[0];
}

public static class Subsets
{
    /// <summary>
    /// Enumerates every size-≥2 subset of <paramref name="baseKeys"/> and key-sweeps each one,
    /// collecting the references at which the subset full-matches an LCM family. A subset
    /// full-matches when all its keys (as ratios 2^(i/k)) bin to good fractions; the match is
    /// strict when every key bins uniquely, otherwise ambiguous (kept only when not strictOnly).
    /// Matches are grouped contiguously per subset, in subset order (size, then keys), and
    /// <paramref name="maxResults"/> caps the number of distinct subsets (rows) returned.
    /// </summary>
    public static (IReadOnlyList<SubsetMatch> Matches, bool Truncated) Enumerate(
        IReadOnlyList<int> baseKeys,
        IReadOnlyList<Fraction> goodFractions,
        int k,
        double binRadius,
        bool strictOnly,
        int maxResults)
    {
        var matches = new List<SubsetMatch>();
        var m = baseKeys.Count;
        if (m < 2 || goodFractions.Count == 0 || k < 1) return (matches, false);

        // All size-≥2 subsets (bitmask power set), sorted by size then keys lexicographically.
        var subsets = new List<List<int>>();
        for (var mask = 1; mask < (1 << m); mask++)
        {
            if (System.Numerics.BitOperations.PopCount((uint)mask) < 2) continue;

            var subsetKeys = new List<int>();
            for (var bit = 0; bit < m; bit++)
            {
                if ((mask & (1 << bit)) != 0) subsetKeys.Add(baseKeys[bit]);
            }
            subsets.Add(subsetKeys);
        }
        subsets.Sort(CompareKeys);

        var rowCount = 0;
        var truncated = false;
        foreach (var subsetKeys in subsets)
        {
            var inputRatios = subsetKeys.Select(i => Math.Pow(2.0, (double)i / k)).ToArray();
            var rows = KeySweep.Compute(inputRatios, goodFractions, k, binRadius);

            var subsetMatches = new List<SubsetMatch>();
            foreach (var row in rows)
            {
                bool strict = row.FullMatch;
                bool ambiguousFull = row.Ambiguous && row.AllInputsBinned;
                if (!strict && (strictOnly || !ambiguousFull)) continue;
                if (row.PostBinLcm is null) continue;

                var lcms = OctaveSweep.CandidateLcmsAscending(row.Cells);
                subsetMatches.Add(new SubsetMatch(subsetKeys, row.KeyIndex, lcms, strict));
            }

            if (subsetMatches.Count == 0) continue;

            if (rowCount >= maxResults) { truncated = true; break; }

            subsetMatches.Sort((a, b) => a.Reference.CompareTo(b.Reference));
            matches.AddRange(subsetMatches);
            rowCount++;
        }

        return (matches, truncated);
    }

    private static int CompareKeys(List<int> a, List<int> b)
    {
        var bySize = a.Count.CompareTo(b.Count);
        if (bySize != 0) return bySize;

        for (var i = 0; i < a.Count; i++)
        {
            var byKey = a[i].CompareTo(b[i]);
            if (byKey != 0) return byKey;
        }

        return 0;
    }
}
