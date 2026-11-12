namespace Melodroid_3.Music;

// Composite placement progressions: the generalisation of a placement progression where origin/target
// key sets are carried not by a single LCM-family placement but by a composite scale (the key-union of
// two bridged placements, CompositeScales). See website/docs/music/progression-principles.mdx
// ("Composite Placement Progression").
//
// Two roles here, both reused by `table progression` and `analyze midi`:
//   * CompositeLabels — name the composite scale a subset triplet (P_A·S·P_B) spans: its stable union
//     P_A ∪ P_B, named via NamedScales when it lands on a catalog scale, else kept as the raw
//     constituents. Deduped per (P_A, P_B) pair since the union ignores which S was chosen.
//   * Escalation / Hosts — when no plain placement progression connects two chords, the move is carried
//     by a composite scale that *hosts* it. Hosts draws from the whole composite universe (named ones as
//     "↑{Short}@{Anchor}", unnamed ones as "↑P_A·S·P_B") plus the non-composite named scales
//     (major / major blues) via NamedScales.ContainingChord. Escalation prefers hosts over the whole
//     origin ∪ target; if nothing hosts both (the dim7 case — {0 3 6 9} is in no placement but is the
//     core of harmonic minor / major), it falls back to hosts over the target (the arrival).
//
// 12-tet by construction (composite scales and their collapse keys are defined mod 12).
public static class CompositeProgressions
{
    // The anchored composite catalog shared by every consumer: all 12 anchors kept (dedupeTransposition
    // off) so containment can be tested at any root, stable unions (the CompositeScales.UnionKeys rule).
    // Single-sources the Enumerate parameters so `table progression` and `analyze midi` agree.
    public static IReadOnlyList<CompositeScale> Catalog(
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        int ktet)
        => CompositeScales.Enumerate(families, minSubsetNotes, stable: true,
            collapseIsomorphic, poolMaximal, dedupeTransposition: false, ktet);

    // The composite scales of a pair's subset triplets. A triplet whose stable union P_A ∪ P_B matches a
    // catalog scale is named ("{Short}@{Anchor}"), deduped by the (Origin, Target) pair — the union
    // ignores S, so the many triplets sharing a pair collapse to one. A triplet whose union matches no
    // catalog scale keeps its full "P_A·S·P_B" constituents, deduped by the whole triplet so distinct S
    // survive. Sorted named scales first (catalog order, then anchor), then unnamed triplets.
    public static IReadOnlyList<string> CompositeLabels(
        IReadOnlyList<ProgressionTriplet> triplets, int ktet)
    {
        var catalogOrder = NamedScales.Catalog
            .Select((s, i) => (s.Name, i))
            .ToDictionary(x => x.Name, x => x.i);

        var labels = new Dictionary<string, (int Rank, int A, int B, int C, int D, string Label)>();
        foreach (var t in triplets.Where(t => t.Principle == ProgressionPrinciple.Subset))
        {
            var union = CompositeScales.UnionKeys(t.Origin, t.Target, stable: true, ktet);
            if (NamedScales.Match(union, ktet) is (string name, int anchor))
            {
                var key = $"{t.Origin.Lcm}@{t.Origin.At}|{t.Target.Lcm}@{t.Target.At}";
                labels[key] = (catalogOrder[name], anchor, 0, 0, 0, $"{NamedScales.Shorthand(name)}@{anchor}");
            }
            else
            {
                var label = TripletLabel(t);
                labels[label] = (catalogOrder.Count,
                    t.Origin.Lcm, t.Origin.At, t.Target.Lcm, t.Target.At, label);
            }
        }

        return labels.Values
            .Distinct()
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.A).ThenBy(x => x.B).ThenBy(x => x.C).ThenBy(x => x.D)
            .ThenBy(x => x.Label, StringComparer.Ordinal)
            .Select(x => x.Label)
            .ToList();
    }

    // The composite scales that can host the move from fromKeys to toKeys when no placement contains
    // both chords. Prefer hosts over the whole origin ∪ target; if nothing hosts both, fall back to
    // hosts over the target (the arrival). Labels carry a leading "↑" to mark the escalation beyond the
    // plain placement graph. Empty iff no composite scale (and no catalog scale) hosts the move.
    public static IReadOnlyList<string> Escalation(
        IReadOnlyList<int> fromKeys,
        IReadOnlyList<int> toKeys,
        IReadOnlyList<CompositeScale> compositeCatalog,
        int ktet)
    {
        var union = fromKeys.Concat(toKeys).Distinct().OrderBy(k => k).ToList();
        var hosts = Hosts(union, compositeCatalog, ktet);
        if (hosts.Count == 0) hosts = Hosts(toKeys, compositeCatalog, ktet);
        return hosts;
    }

    // Named-and-unnamed composite hosts of a key set: named scales containing it (via the catalog, so
    // non-composite scales like major / major blues are included) plus unnamed composites whose keys
    // contain it. Labelled with a leading "↑" and sorted named-first then unnamed.
    private static IReadOnlyList<string> Hosts(
        IReadOnlyList<int> keys, IReadOnlyList<CompositeScale> compositeCatalog, int ktet)
    {
        var catalogOrder = NamedScales.Catalog
            .Select((s, i) => (s.Name, i))
            .ToDictionary(x => x.Name, x => x.i);
        var target = new HashSet<int>(keys);

        var labels = new Dictionary<string, (int Rank, int A, int B, int C, int D, string Label)>();
        foreach (var r in NamedScales.ContainingChord(keys, ktet))
        {
            var label = $"↑{NamedScales.Shorthand(r.Name)}@{r.Anchor}";
            labels[label] = (catalogOrder[r.Name], r.Anchor, 0, 0, 0, label);
        }
        foreach (var j in compositeCatalog)
        {
            if (!target.IsSubsetOf(j.Keys)) continue;
            if (NamedScales.Match(j.Keys, ktet) is not null) continue; // named ones already covered
            var label = "↑" + TripletLabel(new ProgressionTriplet(
                j.PA, j.S, j.PB, ProgressionPrinciple.Subset));
            labels[label] = (catalogOrder.Count, j.PA.Lcm, j.PA.At, j.PB.Lcm, j.PB.At, label);
        }

        return labels.Values
            .Distinct()
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.A).ThenBy(x => x.B).ThenBy(x => x.C).ThenBy(x => x.D)
            .ThenBy(x => x.Label, StringComparer.Ordinal)
            .Select(x => x.Label)
            .ToList();
    }

    // "P_A·S·P_B" for a subset triplet, matching `table composite-scales`' constituent column.
    private static string TripletLabel(ProgressionTriplet t) =>
        $"{t.Origin.Lcm}@{t.Origin.At}·{t.Bridge.Lcm}@{t.Bridge.At}·{t.Target.Lcm}@{t.Target.At}";
}
