namespace Melodroid_3.Music;

// General formalisation of the two chord-progression principles from
// website/docs/music/progression-principles.mdx ("Progression Principles"):
//
//   * Superset principle — chord A may progress to chord B when a single good-LCM family placement
//     contains both (e.g. C→Eb: both sit under 15@7).
//   * Subset principle  — A's containing placement P_A and B's containing placement P_B share a
//     common subset family S (e.g. C→Db: 15@7 and 24@8 both contain 18@10).
//
// A progression is modelled as a triplet of placements (ProgressionTriplet): an origin placement
// containing the origin chord, a target placement containing the target chord, and a bridge
// placement fulfilling a principle. Under the superset principle a single placement contains both
// chords, so origin = bridge = target; under the subset principle the triplet is P_A · S · P_B.
//
// ComputePair(origin, target, …) yields the category and the full triplet list for one ordered pair
// (the standard form). Compute(origin, …) enumerates every major/minor (+ optional dim) triad as a
// candidate target and returns a ProgressionTarget per row (the convenience form); its category is
// identical to ComputePair's for the same pair.
//
// Modeling note — the two containing placements in the subset test must be *distinct*. If P_A = P_B
// were allowed, that single placement already contains both chords (the superset condition) and a
// shared subset S ⊆ P_A trivially exists, so superset would always imply subset and "superset-only"
// could never occur. Requiring distinctness classifies the single-placement case as superset and
// reserves the subset principle for chords sitting in different placements bridged by a shared
// subset. The categories are not mutually exclusive: a pair is `Both` when the superset condition
// holds *and* a distinct pair independently shares a subset.
//
// Categories are always computed from the *raw* placements (the rows of Placements.FindSupersets,
// after any --drop-collapsed filtering, which is semantic), so the displayed triplets never affect
// the category. By default the triplet lists are raw too (every containing placement; every
// P_A·S·P_B triple with plain {Lcm}@{At} labels). Two opt-in, independent declutters shrink them
// losslessly for existence:
//   * poolMaximal — keep only *maximal* containing placements P_A/P_B (by key-set inclusion) and the
//     *maximal* shared subsets S. (If S ⊆ P_A ∩ P_B and P_A ⊆ P_A', then S ⊆ P_A' ∩ P_B' too.)
//   * collapseIsomorphic — relabel each placement to the representative of its key set, preferring a
//     power-of-2 LCM (4 over 3, 8 over 9/10/12), then lowest LCM, then anchor, and dedupe. Isomorphic
//     (and any keyboard-coincident) placements share a key set and so collapse to one label.
//
// 12-tet by construction (the triad shapes are defined mod 12).

public enum TriadQuality { Major, Minor, Diminished }

public enum ProgressionCategory { Neither, Superset, Subset, Both }

public enum ProgressionPrinciple { Superset, Subset }

// Which side's collapsed unstable placements (lcm-15 / lcm-20) to disqualify as bridges: none
// (default), the origin (source-chord) supersets, the target's supersets, or both. See
// Placements.CollapsingKey.
public enum CollapseFilter { None, Origin, Target, Both }

// One progression as a triplet of placements. Superset principle: a single placement contains both
// chords, so Origin = Bridge = Target. Subset principle: Origin = P_A ⊇ origin chord,
// Target = P_B ⊇ target chord, Bridge = a shared subset S ⊆ P_A ∩ P_B.
public readonly record struct ProgressionTriplet(
    Placement Origin, Placement Bridge, Placement Target, ProgressionPrinciple Principle);

// One candidate next chord and how it is (or is not) reachable: the absolute folded key set +
// quality/root, the derived category, and the bridging labels under each principle. Superset
// bridges are placement labels ("{Lcm}@{At}") containing both chords; subset bridges are
// "P_A·S·P_B" triples. Either list may be empty.
public readonly record struct ProgressionTarget(
    IReadOnlyList<int> Keys,
    TriadQuality Quality,
    int Root,
    ProgressionCategory Category,
    IReadOnlyList<string> SupersetBridges,
    IReadOnlyList<string> SubsetBridges);

public static class ChordProgressions
{
    // Root-relative triad shapes (folded pitch classes) — the next-chord candidate universe.
    private static readonly IReadOnlyList<int> MajorShape = new[] { 0, 4, 7 };
    private static readonly IReadOnlyList<int> MinorShape = new[] { 0, 3, 7 };
    private static readonly IReadOnlyList<int> DimShape = new[] { 0, 3, 6 };

    private static int Fold(int key, int ktet) => ((key % ktet) + ktet) % ktet;

    private static IReadOnlyList<int> FoldSet(IEnumerable<int> keys, int ktet) =>
        keys.Select(k => Fold(k, ktet)).Distinct().OrderBy(k => k).ToList();

    private static string Label(Placement p) => $"{p.Lcm}@{p.At}";

    private static bool IsPowerOf2(int n) => n > 0 && (n & (n - 1)) == 0;

    private static string Signature(IReadOnlyCollection<int> keys) =>
        string.Join(",", keys.OrderBy(k => k));

    private static IReadOnlyList<int> Shape(TriadQuality quality) => quality switch
    {
        TriadQuality.Major => MajorShape,
        TriadQuality.Minor => MinorShape,
        _ => DimShape,
    };

    // Major + minor triads over all roots, plus dim when includeDim; each folded and absolute.
    private static IEnumerable<(IReadOnlyList<int> Keys, TriadQuality Quality, int Root)> Candidates(
        bool includeDim, int ktet)
    {
        var qualities = includeDim
            ? new[] { TriadQuality.Major, TriadQuality.Minor, TriadQuality.Diminished }
            : new[] { TriadQuality.Major, TriadQuality.Minor };

        foreach (var quality in qualities)
        {
            var shape = Shape(quality);
            for (var root = 0; root < ktet; root++)
                yield return (FoldSet(shape.Select(k => k + root), ktet), quality, root);
        }
    }

    private readonly record struct P(Placement Placement, IReadOnlySet<int> Keys);

    private static P Wrap(Placement p) => new(p, new HashSet<int>(p.Keys));

    // Placements whose key set is not a proper subset of another item's — the antichain by inclusion.
    private static List<P> Maximal(IReadOnlyList<P> items) =>
        items
            .Where(x => !items.Any(y =>
                y.Keys.Count > x.Keys.Count && x.Keys.IsSubsetOf(y.Keys)))
            .ToList();

    // key-set signature → canonical representative placement, preferring a power-of-2 LCM, then
    // lowest LCM, then lowest anchor. Isomorphic (and any keyboard-coincident) placements share a key
    // set and so collapse to one representative.
    private static IReadOnlyDictionary<string, Placement> BuildCanonicalMap(
        IReadOnlyList<LcmFamily> families, int ktet)
    {
        var map = new Dictionary<string, Placement>();
        foreach (var group in families
            .SelectMany(f => Placements.Sweep(f, ktet))
            .GroupBy(p => Signature(p.Keys)))
        {
            map[group.Key] = group
                .OrderBy(p => IsPowerOf2(p.Lcm) ? 0 : 1)
                .ThenBy(p => p.Lcm)
                .ThenBy(p => p.At)
                .First();
        }
        return map;
    }

    private static Placement Rep(IReadOnlyDictionary<string, Placement> canonical, P p) =>
        canonical[Signature(p.Keys)];

    // Every progression (origin/bridge/target triplet) for one ordered chord pair, plus the derived
    // category. Triplet lists are raw unless collapseIsomorphic / poolMaximal declutter them; neither
    // flag changes the category. --drop-collapsed is applied to the raw placements (it is semantic).
    public static (ProgressionCategory Category, IReadOnlyList<ProgressionTriplet> Triplets) ComputePair(
        IReadOnlyCollection<int> origin,
        IReadOnlyCollection<int> target,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic = false,
        bool poolMaximal = false,
        CollapseFilter dropCollapsed = CollapseFilter.None,
        int ktet = 12)
    {
        var dropOrigin = dropCollapsed is CollapseFilter.Origin or CollapseFilter.Both;
        var dropTarget = dropCollapsed is CollapseFilter.Target or CollapseFilter.Both;

        var supA = OriginSupersets(origin, families, dropOrigin, ktet);
        var subsetCandidates = SubsetCandidates(families, minSubsetNotes, ktet);
        var canonical = collapseIsomorphic ? BuildCanonicalMap(families, ktet) : null;

        return BuildPair(
            supA, target, families, subsetCandidates, minSubsetNotes,
            dropTarget, collapseIsomorphic, poolMaximal, canonical, ktet);
    }

    // Every candidate next chord for the given source chord keys, each tagged superset / subset /
    // both / neither per the two principles above. Sorted by root, then quality. Bridge lists are raw
    // unless collapseIsomorphic / poolMaximal declutter them.
    public static IReadOnlyList<ProgressionTarget> Compute(
        IReadOnlyCollection<int> chordKeys,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool includeDim,
        bool collapseIsomorphic = false,
        bool poolMaximal = false,
        CollapseFilter dropCollapsed = CollapseFilter.None,
        int ktet = 12)
    {
        var dropOrigin = dropCollapsed is CollapseFilter.Origin or CollapseFilter.Both;
        var dropTarget = dropCollapsed is CollapseFilter.Target or CollapseFilter.Both;

        var supA = OriginSupersets(chordKeys, families, dropOrigin, ktet);
        var subsetCandidates = SubsetCandidates(families, minSubsetNotes, ktet);
        var canonical = collapseIsomorphic ? BuildCanonicalMap(families, ktet) : null;

        var results = new List<ProgressionTarget>();
        foreach (var (keys, quality, root) in Candidates(includeDim, ktet))
        {
            var (category, triplets) = BuildPair(
                supA, keys, families, subsetCandidates, minSubsetNotes,
                dropTarget, collapseIsomorphic, poolMaximal, canonical, ktet);

            var supersetBridges = triplets
                .Where(t => t.Principle == ProgressionPrinciple.Superset)
                .Select(t => Label(t.Bridge))
                .Distinct()
                .OrderBy(SortKey)
                .ToList();

            var subsetBridges = triplets
                .Where(t => t.Principle == ProgressionPrinciple.Subset)
                .Select(t => $"{Label(t.Origin)}·{Label(t.Bridge)}·{Label(t.Target)}")
                .Distinct()
                .ToList();

            results.Add(new ProgressionTarget(keys, quality, root, category, supersetBridges, subsetBridges));
        }

        return results
            .OrderBy(t => t.Root)
            .ThenBy(t => t.Quality)
            .ToList();
    }

    // Every (P_A, S, P_B) subset-principle triplet over the *whole* placement universe, with no chord
    // anchoring: distinct placements P_A, P_B sharing a subset S ⊆ P_A ∩ P_B (|S| ≥ minSubsetNotes).
    // Reuses the chord-anchored SubsetTriplets by passing every family placement as both sides — that
    // method makes no assumption that the two lists came from a chord's supersets. Used by
    // JunctionScales to read each triplet as a scale (the key-union of P_A and P_B). Both orderings
    // (P_A,S,P_B) and (P_B,S,P_A) are produced; callers that treat the pair as unordered dedupe.
    public static IReadOnlyList<ProgressionTriplet> SubsetPairs(
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic = false,
        bool poolMaximal = false,
        int ktet = 12)
    {
        var all = families.SelectMany(f => Placements.Sweep(f, ktet)).Select(Wrap).ToList();
        var subsetCandidates = SubsetCandidates(families, minSubsetNotes, ktet);
        var canonical = collapseIsomorphic ? BuildCanonicalMap(families, ktet) : null;
        return SubsetTriplets(
            all, all, subsetCandidates, minSubsetNotes, collapseIsomorphic, poolMaximal, canonical)
            .ToList();
    }

    // Raw placements containing the origin chord (drop-origin filtered when requested). Shared across
    // every candidate target in the convenience form, so computed once by the callers above.
    private static List<P> OriginSupersets(
        IReadOnlyCollection<int> origin, IReadOnlyList<LcmFamily> families, bool dropOrigin, int ktet)
    {
        var placements = Placements.FindSupersets(origin, families, ktet).Select(r => r.Placement).ToList();
        if (dropOrigin) placements = Placements.DropCollapsed(placements, origin, ktet).ToList();
        return placements.Select(Wrap).ToList();
    }

    // Shared-subset S candidates: every family placement with at least minSubsetNotes keys.
    private static List<P> SubsetCandidates(IReadOnlyList<LcmFamily> families, int minSubsetNotes, int ktet) =>
        families
            .SelectMany(f => Placements.Sweep(f, ktet))
            .Select(Wrap)
            .Where(x => x.Keys.Count >= minSubsetNotes)
            .ToList();

    private static (ProgressionCategory Category, IReadOnlyList<ProgressionTriplet> Triplets) BuildPair(
        IReadOnlyList<P> supA,
        IReadOnlyCollection<int> targetKeys,
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<P> subsetCandidates,
        int minSubsetNotes,
        bool dropTarget,
        bool collapseIsomorphic,
        bool poolMaximal,
        IReadOnlyDictionary<string, Placement>? canonical,
        int ktet)
    {
        var targetSet = (IReadOnlySet<int>)new HashSet<int>(targetKeys);

        // Raw placements containing the target B (drop-target filtered — semantic, affects category).
        var supBPlacements = Placements.FindSupersets(targetKeys, families, ktet).Select(r => r.Placement).ToList();
        if (dropTarget) supBPlacements = Placements.DropCollapsed(supBPlacements, targetKeys, ktet).ToList();
        var supB = supBPlacements.Select(Wrap).ToList();

        // Superset principle: a single placement (∈ supA) also contains the target. A superset bridge
        // is played as A ∪ B, so with dropTarget disqualify those collapsed by the target triad too.
        var containingPlacements = supA.Where(pa => targetSet.IsSubsetOf(pa.Keys)).Select(x => x.Placement).ToList();
        if (dropTarget) containingPlacements = Placements.DropCollapsed(containingPlacements, targetKeys, ktet).ToList();
        var containing = containingPlacements.Select(Wrap).ToList();
        var hasSuperset = containing.Count > 0;

        // Subset principle (raw existence): distinct P_A ⊇ A, P_B ⊇ B sharing a subset S (|S| ≥ min).
        var hasSubset = SubsetExists(supA, supB, subsetCandidates, minSubsetNotes);

        var category = (hasSuperset, hasSubset) switch
        {
            (true, true) => ProgressionCategory.Both,
            (true, false) => ProgressionCategory.Superset,
            (false, true) => ProgressionCategory.Subset,
            _ => ProgressionCategory.Neither,
        };

        var triplets = new List<ProgressionTriplet>();
        triplets.AddRange(SupersetTriplets(containing, collapseIsomorphic, poolMaximal, canonical));
        triplets.AddRange(SubsetTriplets(supA, supB, subsetCandidates, minSubsetNotes, collapseIsomorphic, poolMaximal, canonical));
        return (category, triplets);
    }

    // Superset triplets: each containing placement P → (P, P, P). poolMaximal keeps only the maximal
    // ones; collapseIsomorphic relabels each to its representative and dedupes by key set.
    private static IEnumerable<ProgressionTriplet> SupersetTriplets(
        IReadOnlyList<P> containing, bool collapseIsomorphic, bool poolMaximal,
        IReadOnlyDictionary<string, Placement>? canonical)
    {
        var display = poolMaximal ? Maximal(containing) : containing;
        if (!collapseIsomorphic)
            return display.Select(p => new ProgressionTriplet(
                p.Placement, p.Placement, p.Placement, ProgressionPrinciple.Superset));

        var triplets = new List<ProgressionTriplet>();
        var seen = new HashSet<string>();
        foreach (var p in display)
        {
            var rep = Rep(canonical!, p);
            if (seen.Add(Signature(rep.Keys)))
                triplets.Add(new ProgressionTriplet(rep, rep, rep, ProgressionPrinciple.Superset));
        }
        return triplets;
    }

    // Subset triplets: distinct P_A ⊇ A, P_B ⊇ B sharing S ⊆ P_A ∩ P_B (|S| ≥ min) → (P_A, S, P_B).
    // poolMaximal keeps only maximal P_A/P_B and maximal S; collapseIsomorphic relabels each slot to
    // its representative (and treats placements that collapse to one key set as the same placement).
    private static IEnumerable<ProgressionTriplet> SubsetTriplets(
        IReadOnlyList<P> supA, IReadOnlyList<P> supB, IReadOnlyList<P> subsetCandidates,
        int minSubsetNotes, bool collapseIsomorphic, bool poolMaximal,
        IReadOnlyDictionary<string, Placement>? canonical)
    {
        var aSide = poolMaximal ? Maximal(supA) : supA.ToList();
        var bSide = poolMaximal ? Maximal(supB) : supB.ToList();

        var triplets = new List<ProgressionTriplet>();
        var seen = new HashSet<string>();
        foreach (var pa in aSide)
        {
            foreach (var pb in bSide)
            {
                if (collapseIsomorphic)
                {
                    // Collapsed-coincident P_A/P_B are the same placement (a superset bridge), not a
                    // subset pair — their shared key set is handled by the superset principle.
                    if (Signature(Rep(canonical!, pa).Keys) == Signature(Rep(canonical!, pb).Keys)) continue;
                }
                else if (pa.Placement.Lcm == pb.Placement.Lcm && pa.Placement.At == pb.Placement.At)
                {
                    continue;
                }

                var intersection = new HashSet<int>(pa.Keys);
                intersection.IntersectWith(pb.Keys);
                if (intersection.Count < minSubsetNotes) continue;

                var fitting = subsetCandidates.Where(s => s.Keys.IsSubsetOf(intersection)).ToList();
                foreach (var s in poolMaximal ? Maximal(fitting) : fitting)
                {
                    var po = collapseIsomorphic ? Rep(canonical!, pa) : pa.Placement;
                    var ps = collapseIsomorphic ? Rep(canonical!, s) : s.Placement;
                    var pt = collapseIsomorphic ? Rep(canonical!, pb) : pb.Placement;
                    var key = $"{Label(po)}·{Label(ps)}·{Label(pt)}";
                    if (seen.Add(key))
                        triplets.Add(new ProgressionTriplet(po, ps, pt, ProgressionPrinciple.Subset));
                }
            }
        }
        return triplets;
    }

    private static bool SubsetExists(
        IReadOnlyList<P> supA, IReadOnlyList<P> supB, IReadOnlyList<P> subsetCandidates, int minSubsetNotes)
    {
        foreach (var pa in supA)
        {
            foreach (var pb in supB)
            {
                if (pa.Placement.Lcm == pb.Placement.Lcm && pa.Placement.At == pb.Placement.At) continue;

                var intersection = new HashSet<int>(pa.Keys);
                intersection.IntersectWith(pb.Keys);
                if (intersection.Count < minSubsetNotes) continue;

                if (subsetCandidates.Any(s => s.Keys.IsSubsetOf(intersection))) return true;
            }
        }
        return false;
    }

    // Sort key for a single "{Lcm}@{At}" label: by LCM then anchor.
    private static (int Lcm, int At) SortKey(string label)
    {
        var parts = label.Split('@');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }
}
