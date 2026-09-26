namespace Melodroid_3.Music;

// General formalisation of the two chord-progression principles from
// website/docs/music/composition-adjacency-and-preference.mdx ("On Chord Progressions"):
//
//   * Superset principle — chord A may progress to chord B when a single good-LCM family placement
//     contains both (e.g. C→Eb: both sit under 15@7).
//   * Subset principle  — A's containing placement P_A and B's containing placement P_B share a
//     common subset family S (e.g. C→Db: 15@7 and 24@8 both contain 18@10).
//
// Unlike `progression` (ChordProgressions), this does no 12-tet preference collapsing: the
// containing placements are the *raw* rows of Placements.FindSupersets (every good-LCM family
// placement whose keys ⊇ the chord), and any good-LCM family of at least `minSubsetNotes` keys may
// serve as the shared subset S.
//
// Modeling note — the two containing placements in the subset test must be *distinct*
// ((Lcm, At) ≠ (Lcm, At)). If P_A = P_B were allowed, that single placement already contains both
// chords (the superset condition) and a shared subset S ⊆ P_A trivially exists, so superset would
// always imply subset and "superset-only" could never occur. Requiring distinctness classifies the
// single-placement case as superset and reserves the subset principle for chords sitting in
// different placements bridged by a shared subset. The categories are not mutually exclusive: a
// target is `Both` when the superset condition holds *and* a distinct pair independently shares a
// subset. S is not required to be a proper subset beyond the distinctness of P_A and P_B.
//
// Every candidate target is returned (including `Neither`) so the full major/minor (+ optional dim)
// matrix can be inspected. 12-tet by construction (the triad shapes are defined mod 12).
//
// Via output — the two category booleans are always computed from the *raw* placements (existence),
// so the displayed bridge lists never affect the category. By default those lists are decluttered:
//   * isomorphism-collapsed — isomorphic families produce identical key sets at shifted anchors
//     (3@7 and 4@0 are both {0,4,7}), so every placement is relabelled to the representative of its
//     key set, preferring a power-of-2 LCM (4 over 3, 8 over 9/10/12), then lowest LCM, then anchor;
//   * pooled upward — only *maximal* containing placements P_A/P_B (by key-set inclusion) and the
//     *maximal* shared subsets S are kept. Pooling is lossless for existence — if S ⊆ P_A ∩ P_B and
//     P_A ⊆ P_A', then S ⊆ P_A' ∩ P_B' too — so it only removes redundant, subsumed labels.
// `rawBridges` restores the full un-collapsed lists (every containing placement; every P_A·S·P_B
// triple with plain {Lcm}@{At} labels).

public enum ProgressionCategory { Neither, Superset, Subset, Both }

// One candidate next chord and how it is (or is not) reachable: the absolute folded key set +
// quality/root, the derived category, and the bridging labels under each principle. Superset
// bridges are placement labels ("{Lcm}@{At}") containing both chords; subset bridges are
// "P_A·S·P_B" triples. Either list may be empty.
public readonly record struct PrincipleTarget(
    IReadOnlyList<int> Keys,
    TriadQuality Quality,
    int Root,
    ProgressionCategory Category,
    IReadOnlyList<string> SupersetBridges,
    IReadOnlyList<string> SubsetBridges);

public static class PrincipleProgressions
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

    // Placements whose key set is not a proper subset of another item's — the antichain by inclusion.
    private static List<P> Maximal(IReadOnlyList<P> items) =>
        items
            .Where(x => !items.Any(y =>
                y.Keys.Count > x.Keys.Count && x.Keys.IsSubsetOf(y.Keys)))
            .ToList();

    // key-set signature → canonical placement label, preferring a power-of-2 LCM, then lowest LCM,
    // then lowest anchor. Isomorphic (and any keyboard-coincident) placements share a key set and so
    // collapse to one label.
    private static IReadOnlyDictionary<string, string> BuildCanonicalMap(
        IReadOnlyList<LcmFamily> families, int ktet)
    {
        var map = new Dictionary<string, string>();
        foreach (var group in families
            .SelectMany(f => Placements.Sweep(f, ktet))
            .GroupBy(p => Signature(p.Keys)))
        {
            var rep = group
                .OrderBy(p => IsPowerOf2(p.Lcm) ? 0 : 1)
                .ThenBy(p => p.Lcm)
                .ThenBy(p => p.At)
                .First();
            map[group.Key] = Label(rep);
        }
        return map;
    }

    private static string Canonical(IReadOnlyDictionary<string, string> map, P placement) =>
        map[Signature((IReadOnlyCollection<int>)placement.Keys)];

    // Every candidate next chord for the given source chord keys, each tagged superset / subset /
    // both / neither per the two principles above. Sorted by root, then quality. Bridge lists are
    // decluttered (isomorphism-collapsed + pooled) unless rawBridges is set.
    public static IReadOnlyList<PrincipleTarget> Compute(
        IReadOnlyCollection<int> chordKeys,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool includeDim,
        bool rawBridges = false,
        int ktet = 12)
    {
        // Placements containing the source chord A (raw — every good-LCM family placement ⊇ A).
        var supA = Placements.FindSupersets(chordKeys, families, ktet)
            .Select(r => new P(r.Placement, new HashSet<int>(r.Placement.Keys)))
            .ToList();

        // Shared-subset S candidates: every family placement with at least minSubsetNotes keys.
        var subsetCandidates = families
            .SelectMany(f => Placements.Sweep(f, ktet))
            .Select(p => new P(p, new HashSet<int>(p.Keys)))
            .Where(x => x.Keys.Count >= minSubsetNotes)
            .ToList();

        var canonical = rawBridges ? null : BuildCanonicalMap(families, ktet);
        var maxA = rawBridges ? supA : Maximal(supA);

        var results = new List<PrincipleTarget>();
        foreach (var (keys, quality, root) in Candidates(includeDim, ktet))
        {
            var targetSet = (IReadOnlySet<int>)new HashSet<int>(keys);

            var supB = Placements.FindSupersets(keys, families, ktet)
                .Select(r => new P(r.Placement, new HashSet<int>(r.Placement.Keys)))
                .ToList();

            // Superset principle: a single placement (∈ supA) contains both A and B.
            var containing = supA.Where(pa => targetSet.IsSubsetOf(pa.Keys)).ToList();
            var hasSuperset = containing.Count > 0;

            // Subset principle: distinct P_A ⊇ A, P_B ⊇ B sharing a subset family S (|S| ≥ min).
            var hasSubset = SubsetExists(supA, supB, subsetCandidates, minSubsetNotes);

            var category = (hasSuperset, hasSubset) switch
            {
                (true, true) => ProgressionCategory.Both,
                (true, false) => ProgressionCategory.Superset,
                (false, true) => ProgressionCategory.Subset,
                _ => ProgressionCategory.Neither,
            };

            var supersetBridges = rawBridges
                ? containing.Select(pa => Label(pa.Placement)).Distinct().ToList()
                : Maximal(containing).Select(pa => Canonical(canonical!, pa)).Distinct().OrderBy(SortKey).ToList();

            var subsetBridges = rawBridges
                ? RawSubsetTriples(supA, supB, subsetCandidates, minSubsetNotes)
                : PooledSubsetTriples(maxA, Maximal(supB), subsetCandidates, minSubsetNotes, canonical!);

            results.Add(new PrincipleTarget(keys, quality, root, category, supersetBridges, subsetBridges));
        }

        return results
            .OrderBy(t => t.Root)
            .ThenBy(t => t.Quality)
            .ToList();
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

    private static List<string> RawSubsetTriples(
        IReadOnlyList<P> supA, IReadOnlyList<P> supB, IReadOnlyList<P> subsetCandidates, int minSubsetNotes)
    {
        var bridges = new List<string>();
        var seen = new HashSet<string>();
        foreach (var pa in supA)
        {
            foreach (var pb in supB)
            {
                if (pa.Placement.Lcm == pb.Placement.Lcm && pa.Placement.At == pb.Placement.At) continue;

                var intersection = new HashSet<int>(pa.Keys);
                intersection.IntersectWith(pb.Keys);
                if (intersection.Count < minSubsetNotes) continue;

                foreach (var s in subsetCandidates)
                {
                    if (!s.Keys.IsSubsetOf(intersection)) continue;
                    var label = $"{Label(pa.Placement)}·{Label(s.Placement)}·{Label(pb.Placement)}";
                    if (seen.Add(label)) bridges.Add(label);
                }
            }
        }
        return bridges;
    }

    private static List<string> PooledSubsetTriples(
        IReadOnlyList<P> maxA, IReadOnlyList<P> maxB, IReadOnlyList<P> subsetCandidates,
        int minSubsetNotes, IReadOnlyDictionary<string, string> canonical)
    {
        var bridges = new List<string>();
        var seen = new HashSet<string>();
        foreach (var pa in maxA)
        {
            var la = Canonical(canonical, pa);
            foreach (var pb in maxB)
            {
                var lb = Canonical(canonical, pb);
                if (la == lb) continue; // isomorphic-coincident: that single placement is a superset bridge

                var intersection = new HashSet<int>(pa.Keys);
                intersection.IntersectWith(pb.Keys);
                if (intersection.Count < minSubsetNotes) continue;

                var fitting = subsetCandidates.Where(s => s.Keys.IsSubsetOf(intersection)).ToList();
                foreach (var s in Maximal(fitting))
                {
                    var label = $"{la}·{Canonical(canonical, s)}·{lb}";
                    if (seen.Add(label)) bridges.Add(label);
                }
            }
        }
        bridges.Sort(StringComparer.Ordinal);
        return bridges;
    }

    // Sort key for a single "{Lcm}@{At}" label: by LCM then anchor.
    private static (int Lcm, int At) SortKey(string label)
    {
        var parts = label.Split('@');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }
}
