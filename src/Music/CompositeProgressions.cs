namespace Melodroid_3.Music;

// Composite placement progressions: the generalisation of a placement progression where origin/target
// key sets are carried not by a single LCM-family placement but by a composite scale (the key-union of
// two bridged placements, CompositeScales). See website/docs/music/progression-principles.mdx
// ("Composite Placement Progression"):
//
//   "A Composite Placement Progression maps the notes at one pulse to a composite scale, the notes of a
//    consecutive pulse to another composite scale, and links them by a placement progression between
//    their constituent placements."
//
// The one calculation, ComputeComposite, implements exactly that and is shared by `table progression`
// (its composite tier) and `analyze midi` (its bridges), so both compute cross-key-set bridges
// identically — the underlying theory is the same:
//   1. ConstituentPool(keys) gathers the proper (single-LCM-family) placements the key set maps to:
//      every single placement containing it (a degenerate composite = a single placement, the special
//      case the doc names) PLUS the constituents (P_A/S/P_B) of every derived composite scale
//      containing it.
//   2. LinkPools links the origin pool to the target pool by a placement progression: a placement
//      shared by both pools (a pivot → superset shape) and/or two placements bridged by a shared subset
//      S (ChordProgressions.SubsetTripletsBetween → P_A·S·P_B). The category follows the same
//      (hasSuperset, hasSubset) grid as a plain placement progression.
//
// CompositeLabels names the composite SCALE a subset triplet spans (for the `--name-composites` display
// on the placement tier); the linking itself never depends on names.
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

    // The constituent pool of a key set: every proper placement the notes "map to a composite scale"
    // through. That is the single placements containing the key set (each a degenerate composite whose
    // sole constituent is itself) plus the P_A/S/P_B of every derived composite scale containing it.
    // Deduped by (Lcm, At). Pass a prebuilt compositeCatalog to share it across many calls.
    public static IReadOnlyList<Placement> ConstituentPool(
        IReadOnlyCollection<int> keys,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        int ktet,
        IReadOnlyList<CompositeScale>? compositeCatalog = null)
    {
        var pooled = new Dictionary<(int Lcm, int At), Placement>();
        foreach (var row in Placements.FindSupersets(keys, families, ktet))
            pooled[(row.Placement.Lcm, row.Placement.At)] = row.Placement;

        var catalog = compositeCatalog ?? Catalog(families, minSubsetNotes, false, false, ktet);
        var chord = new HashSet<int>(keys.Select(k => ((k % ktet) + ktet) % ktet));
        foreach (var c in catalog)
        {
            if (!chord.IsSubsetOf(c.Keys)) continue;
            foreach (var p in new[] { c.PA, c.S, c.PB })
                pooled[(p.Lcm, p.At)] = p;
        }
        return pooled.Values.ToList();
    }

    // Link an origin constituent pool to a target constituent pool by a placement progression. Pivots
    // (placements in both pools) are the superset shape (Origin = Bridge = Target); subset triplets
    // (ChordProgressions.SubsetTripletsBetween) are the bridged shape P_A·S·P_B. Category follows the
    // same (hasSuperset, hasSubset) → Neither/Superset/Subset/Both grid as a plain placement progression.
    public static (ProgressionCategory Category, IReadOnlyList<ProgressionTriplet> Triplets) LinkPools(
        IReadOnlyList<Placement> originPool,
        IReadOnlyList<Placement> targetPool,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        int ktet)
    {
        var targetKeys = new HashSet<(int Lcm, int At)>(targetPool.Select(p => (p.Lcm, p.At)));
        var seenPivot = new HashSet<(int Lcm, int At)>();
        var pivots = new List<ProgressionTriplet>();
        foreach (var p in originPool.OrderBy(p => p.Lcm).ThenBy(p => p.At))
            if (targetKeys.Contains((p.Lcm, p.At)) && seenPivot.Add((p.Lcm, p.At)))
                pivots.Add(new ProgressionTriplet(p, p, p, ProgressionPrinciple.Superset));

        var subset = ChordProgressions.SubsetTripletsBetween(
            originPool, targetPool, families, minSubsetNotes, collapseIsomorphic, poolMaximal, ktet);

        var category = (pivots.Count > 0, subset.Count > 0) switch
        {
            (true, true) => ProgressionCategory.Both,
            (true, false) => ProgressionCategory.Superset,
            (false, true) => ProgressionCategory.Subset,
            _ => ProgressionCategory.Neither,
        };
        return (category, pivots.Concat(subset).ToList());
    }

    // The composite placement progression between two key sets: link their constituent pools. The plain
    // placement progression is the special case where each pool is a single placement. Shared by
    // `table progression` (composite tier) and `analyze midi` (bridges) so both bridge identically.
    public static (ProgressionCategory Category, IReadOnlyList<ProgressionTriplet> Triplets) ComputeComposite(
        IReadOnlyCollection<int> originKeys,
        IReadOnlyCollection<int> targetKeys,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        int ktet,
        IReadOnlyList<CompositeScale>? compositeCatalog = null)
    {
        var catalog = compositeCatalog ?? Catalog(families, minSubsetNotes, false, false, ktet);
        var originPool = ConstituentPool(originKeys, families, minSubsetNotes, ktet, catalog);
        var targetPool = ConstituentPool(targetKeys, families, minSubsetNotes, ktet, catalog);
        return LinkPools(originPool, targetPool, families, minSubsetNotes, collapseIsomorphic, poolMaximal, ktet);
    }

    // The composite scales a pair's subset triplets span. A triplet whose stable union P_A ∪ P_B matches a
    // catalog scale is named ("{Short}@{Anchor}"), deduped by the (Origin, Target) pair — the union
    // ignores S, so the many triplets sharing a pair collapse to one. A triplet whose union matches no
    // catalog scale keeps its full "P_A·S·P_B" constituents, deduped by the whole triplet so distinct S
    // survive. Sorted named scales first (catalog order, then anchor), then unnamed triplets. Display
    // only (the `--name-composites` relabel); the bridge calculation never depends on it.
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

    // "P_A·S·P_B" for a subset triplet, matching `table composite-scales`' constituent column.
    private static string TripletLabel(ProgressionTriplet t) =>
        $"{t.Origin.Lcm}@{t.Origin.At}·{t.Bridge.Lcm}@{t.Bridge.At}·{t.Target.Lcm}@{t.Target.At}";
}
