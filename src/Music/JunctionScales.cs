namespace Melodroid_3.Music;

// A junction scale: a useful non-LCM scale formed by reading the subset principle
// (website/docs/music/progression-principles.mdx) as a scale rather than a chord progression. Two
// distinct placements P_A, P_B that share a common LCM-family subset S (|S| ≥ minSubsetNotes) are
// unioned into a playable key set. Keys is the concrete 12-tet key set to play; PA, S, PB are the
// constituents it was generated from.
//
// The union uses the *stable* form of any collapsing (prime-5: lcm-15/20) constituent and then drops
// every collapsing constituent's collapse key from the union — not merely from that constituent's
// own keys. This matters for harmonic minor: 15@11's stable form drops key 7, but 24@0 independently
// contains 7, so only subtracting it from the finished union recovers harm@9 = {0 2 4 5 8 9 11}.
//
// Blues is a *looser* member and is NOT reproduced by the union rule: 24@9 ∪ 24@2 = {1 2 4 6 7 8 9 11},
// whereas blues@4 = {1 4 6 7 8 11} keeps strictly fewer keys (neither 24 collapses, so no rule drops
// 2 and 9). The command emits the union (a superset of blues); blues is a manual sub-selection.
//
// 12-tet by construction (the subset principle and collapse keys are defined mod 12).
public readonly record struct JunctionScale(
    IReadOnlyList<int> Keys, Placement PA, Placement S, Placement PB);

public static class JunctionScales
{
    // Enumerate every junction scale over the given families and all anchors.
    //   minSubsetNotes      — |S| lower bound (default 4, so S is a substantial 18/8 parent; harm's
    //                         S=18 has 6 keys, blues' shared subset has 4).
    //   stable              — use collapsing constituents' stable form and subtract their collapse
    //                         keys from the union (default true; harm@9 requires it).
    //   collapseIsomorphic / poolMaximal — mirror ChordProgressions.SubsetPairs' label declutters.
    //   dedupeTransposition — fold the 12 transpositions of a scale to one representative (default on).
    public static IReadOnlyList<JunctionScale> Enumerate(
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes = 4,
        bool stable = true,
        bool collapseIsomorphic = false,
        bool poolMaximal = false,
        bool dedupeTransposition = true,
        int ktet = 12)
    {
        var triplets = ChordProgressions.SubsetPairs(
            families, minSubsetNotes, collapseIsomorphic, poolMaximal, ktet);

        var scales = new List<JunctionScale>();
        var seen = new HashSet<string>();
        foreach (var t in triplets)
        {
            // (P_A,S,P_B) and (P_B,S,P_A) are the same junction; order the outer pair so they dedupe.
            var (pa, pb) = Order(t.Origin, t.Target);
            var keys = UnionKeys(pa, pb, stable, ktet);
            var signature = $"{KeySig(keys)}|{Label(pa)}|{Label(t.Bridge)}|{Label(pb)}";
            if (seen.Add(signature))
                scales.Add(new JunctionScale(keys, pa, t.Bridge, pb));
        }

        var result = dedupeTransposition ? DedupeTransposition(scales, ktet) : scales;
        result = result.ToList();
        ((List<JunctionScale>)result).Sort(Compare);
        return result;
    }

    // The playable key set: union of the two constituents' (optionally stable) key sets, with each
    // collapsing constituent's collapse key removed from the union. Public so callers that frame a
    // subset bridge as a scale (e.g. SongAnalysis' --name-junctions) reuse the one stable-union rule
    // rather than re-deriving it.
    public static IReadOnlyList<int> UnionKeys(Placement pa, Placement pb, bool stable, int ktet)
    {
        var union = new HashSet<int>(stable ? StableKeys(pa, ktet) : pa.Keys);
        union.UnionWith(stable ? StableKeys(pb, ktet) : pb.Keys);
        if (stable)
        {
            if (Placements.CollapsingKey(pa, ktet) is int ca) union.Remove(ca);
            if (Placements.CollapsingKey(pb, ktet) is int cb) union.Remove(cb);
        }
        return union.OrderBy(k => k).ToList();
    }

    // Stable key set of one placement. lcm-15 delegates to Placements.StableFifteen (its documented
    // scope). lcm-20 also collapses (Placements.CollapsingKey fires) but StableFifteen leaves it
    // untouched by design, so drop its collapse key here. Everything else passes through.
    private static IReadOnlyList<int> StableKeys(Placement p, int ktet)
    {
        if (p.Lcm == 15)
            return Placements.StableFifteen(new[] { p }, ktet)[0].Keys;
        if (p.Lcm == 20 && Placements.CollapsingKey(p, ktet) is int ck)
            return p.Keys.Where(k => k != ck).ToList();
        return p.Keys;
    }

    // Keep one representative per transposition class (necklace), chosen by lowest constituent LCMs/
    // anchors, then the largest bridge. The representative's concrete anchored Keys are emitted as-is.
    private static IReadOnlyList<JunctionScale> DedupeTransposition(
        IReadOnlyList<JunctionScale> scales, int ktet)
    {
        var best = new Dictionary<string, JunctionScale>();
        foreach (var s in scales)
        {
            var necklace = KeySig(Chords.Canonicalize(s.Keys, ktet).Keys);
            if (!best.TryGetValue(necklace, out var current) || RepRank(s).CompareTo(RepRank(current)) < 0)
                best[necklace] = s;
        }
        return best.Values.ToList();
    }

    private static (Placement, Placement) Order(Placement a, Placement b) =>
        (a.Lcm, a.At).CompareTo((b.Lcm, b.At)) <= 0 ? (a, b) : (b, a);

    private static (int, int, int, int, int) RepRank(JunctionScale s) =>
        (s.PA.Lcm, s.PA.At, s.PB.Lcm, s.PB.At, -s.S.Keys.Count);

    private static int Compare(JunctionScale a, JunctionScale b)
    {
        if (a.Keys.Count != b.Keys.Count) return a.Keys.Count.CompareTo(b.Keys.Count);
        for (var i = 0; i < a.Keys.Count; i++)
            if (a.Keys[i] != b.Keys[i]) return a.Keys[i].CompareTo(b.Keys[i]);
        return RepRank(a).CompareTo(RepRank(b));
    }

    private static string Label(Placement p) => $"{p.Lcm}@{p.At}";

    private static string KeySig(IReadOnlyList<int> keys) => string.Join(",", keys);
}
