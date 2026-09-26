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

    // Every candidate next chord for the given source chord keys, each tagged superset / subset /
    // both / neither per the two principles above. Sorted by root, then quality.
    public static IReadOnlyList<PrincipleTarget> Compute(
        IReadOnlyCollection<int> chordKeys,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool includeDim,
        int ktet = 12)
    {
        // Placements containing the source chord A (raw — every good-LCM family placement ⊇ A).
        var supA = Placements.FindSupersets(chordKeys, families, ktet)
            .Select(r => (r.Placement, Keys: (IReadOnlySet<int>)new HashSet<int>(r.Placement.Keys)))
            .ToList();

        // Shared-subset S candidates: every family placement with at least minSubsetNotes keys.
        var subsetCandidates = families
            .SelectMany(f => Placements.Sweep(f, ktet))
            .Select(p => (Placement: p, Keys: (IReadOnlySet<int>)new HashSet<int>(p.Keys)))
            .Where(x => x.Keys.Count >= minSubsetNotes)
            .ToList();

        var results = new List<PrincipleTarget>();
        foreach (var (keys, quality, root) in Candidates(includeDim, ktet))
        {
            var targetSet = new HashSet<int>(keys);

            // Superset principle: a single placement (∈ supA) contains both A and B.
            var supersetBridges = supA
                .Where(pa => targetSet.IsSubsetOf(pa.Keys))
                .Select(pa => Label(pa.Placement))
                .Distinct()
                .ToList();

            // Subset principle: distinct P_A ⊇ A, P_B ⊇ B sharing a subset family S (|S| ≥ min).
            var supB = Placements.FindSupersets(keys, families, ktet)
                .Select(r => (r.Placement, Keys: (IReadOnlySet<int>)new HashSet<int>(r.Placement.Keys)))
                .ToList();

            var subsetBridges = new List<string>();
            var seenBridge = new HashSet<string>();
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
                        if (seenBridge.Add(label)) subsetBridges.Add(label);
                    }
                }
            }

            var category = (supersetBridges.Count > 0, subsetBridges.Count > 0) switch
            {
                (true, true) => ProgressionCategory.Both,
                (true, false) => ProgressionCategory.Superset,
                (false, true) => ProgressionCategory.Subset,
                _ => ProgressionCategory.Neither,
            };

            results.Add(new PrincipleTarget(keys, quality, root, category, supersetBridges, subsetBridges));
        }

        return results
            .OrderBy(t => t.Root)
            .ThenBy(t => t.Quality)
            .ToList();
    }
}
