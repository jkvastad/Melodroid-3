namespace Melodroid_3.Music;

// Perception-based chord progression logic: which major/minor/dim triads a given chord (any set of
// 12-tet keys) may progress to. The source chord's direct supersets are *derived* from the placement
// math — exactly the rows of
//   chord-melody --drop-renormalized-subsets --drop-collapsed --stable-15
// (see Placements.FindMaximalContaining / DropCollapsed / StableFifteen). Adjacency then follows
// from those.
//
// Two walks:
//   * default (strict): bidirectional 15↔24 adjacency — each direct lcm-15 row reaches the adjacent
//     lcm-24 placements 24@(At+1),(At+8) and each direct lcm-24 row reaches the adjacent lcm-15
//     placements 15@(At+4),(At+11). A target is reachable when it is a subset of a direct superset
//     or of any adjacency placement. This reproduces the four progression tables in
//     website/docs/music/composition-adjacency-and-preference.mdx (e.g. a major source reaches every
//     major triad but the tritone): the 4@k ↔ 3@(k+7) isomorphism falls out of the uniform subset
//     test, since 4@k is a literal subset of the full 15@(k+7) reached via 24→15 adjacency.
//   * --stable: the narrower one-directional walk — adjacency only from stable lcm-15 rows to their
//     two adjacent lcm-24 placements.
//
// This mirrors the "Perception Based Progression" derivation in website/docs/music. The command is
// inherently 12-tet — the 15s stabilisation and the adjacency rules are defined mod 12.

public enum TriadQuality { Major, Minor, Diminished }

// One reachable next chord and how it is reached: the absolute folded key set + quality/root, and
// the bridging superset placement labels under each rule (either list may be empty).
public readonly record struct ProgressionTarget(
    IReadOnlyList<int> Keys,
    TriadQuality Quality,
    int Root,
    IReadOnlyList<string> SupersetBridges,
    IReadOnlyList<string> AdjacencyBridges);

public static class ChordProgressions
{
    private const int Ktet = 12;

    // Root-relative triad shapes (folded pitch classes) — the next-chord candidate universe.
    private static readonly IReadOnlyList<int> MajorShape = new[] { 0, 4, 7 };
    private static readonly IReadOnlyList<int> MinorShape = new[] { 0, 3, 7 };
    private static readonly IReadOnlyList<int> DimShape = new[] { 0, 3, 6 };

    private static IReadOnlyList<int> Shape(TriadQuality quality) => quality switch
    {
        TriadQuality.Major => MajorShape,
        TriadQuality.Minor => MinorShape,
        _ => DimShape,
    };

    private static int Fold(int key) => ((key % Ktet) + Ktet) % Ktet;

    private static IReadOnlyList<int> FoldSet(IEnumerable<int> keys) =>
        keys.Select(Fold).Distinct().OrderBy(k => k).ToList();

    // Row label matching ChordMelodyTableRenderer: a stabilized lcm-15 placement (its collapsing
    // key dropped by StableFifteen) reads as "15s@At"; every other placement as "{Lcm}@{At}".
    private static string PlacementLabel(Placement placement) =>
        Placements.CollapsingKey(placement, Ktet) is int ck && !placement.Keys.Contains(ck)
            ? $"15s@{placement.At}"
            : $"{placement.Lcm}@{placement.At}";

    // The 36 major/minor/dim triads (absolute, folded) — the next-chord candidate universe.
    private static IEnumerable<(IReadOnlyList<int> Keys, TriadQuality Quality, int Root)> AllTriads()
    {
        foreach (var quality in new[] { TriadQuality.Major, TriadQuality.Minor, TriadQuality.Diminished })
        {
            var shape = Shape(quality);
            for (var root = 0; root < Ktet; root++)
                yield return (FoldSet(shape.Select(k => k + root)), quality, root);
        }
    }

    // Every next-chord target for the given chord keys: each major/minor/dim triad that is a subset
    // of one of the chord's direct supersets (the superset rule) or of an adjacency placement (the
    // adjacency rule). Both bridge lists are populated; a target is included when at least one rule
    // reaches it. When `stable` is true the narrower one-directional walk is used (adjacency only
    // from stable lcm-15 rows to lcm-24); otherwise the default strict bidirectional walk applies.
    // Sorted by root, then quality.
    public static IReadOnlyList<ProgressionTarget> Compute(
        IReadOnlyCollection<int> chordKeys,
        bool stable,
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<FamilyRelation> relations,
        LcmFamily lcm24Family,
        LcmFamily lcm15Family)
    {
        // Direct supersets = maximal placements containing the chord, with collapsed lcm-15 rows
        // dropped and the rest reduced to stable 15s form. Mirrors the chord-melody flags
        // --drop-renormalized-subsets --drop-collapsed --stable-15.
        var direct = Placements.FindMaximalContaining(chordKeys, families, relations, Ktet, dropRenormalizedSubsets: true);
        direct = Placements.DropCollapsed(direct, chordKeys, Ktet);
        direct = Placements.StableFifteen(direct, Ktet);

        var supersets = direct
            .Select(p => (Keys: new HashSet<int>(FoldSet(p.Keys)), Label: PlacementLabel(p)))
            .ToList();

        // Adjacency placements. Both walks send each direct lcm-15 row 15@At to the adjacent lcm-24
        // placements 24@(At+1),(At+8). The strict (default) walk is additionally bidirectional: each
        // direct lcm-24 row 24@At also reaches the adjacent lcm-15 placements 15@(At+4),(At+11). All
        // are materialised as full family placements (mod 12) and de-duplicated by (lcm, at).
        var adjacencySeen = new HashSet<(int Lcm, int At)>();
        var adjacency = new List<(HashSet<int> Keys, string Label)>();
        void AddAdjacency(LcmFamily family, int at)
        {
            at = Fold(at);
            if (!adjacencySeen.Add((family.Lcm, at))) return;
            var keys = new HashSet<int>(FoldSet(Placements.Compute(family, at, Ktet).Keys));
            adjacency.Add((keys, $"{family.Lcm}@{at}"));
        }
        foreach (var p in direct.Where(p => p.Lcm == 15))
        {
            AddAdjacency(lcm24Family, p.At + 1);
            AddAdjacency(lcm24Family, p.At + 8);
        }
        if (!stable)
        {
            foreach (var p in direct.Where(p => p.Lcm == 24))
            {
                AddAdjacency(lcm15Family, p.At + 4);
                AddAdjacency(lcm15Family, p.At + 11);
            }
        }

        var targets = new List<ProgressionTarget>();
        foreach (var (keys, cQuality, cRoot) in AllTriads())
        {
            var supersetBridges = supersets.Where(s => keys.All(s.Keys.Contains)).Select(s => s.Label).ToList();
            var adjacencyBridges = adjacency.Where(s => keys.All(s.Keys.Contains)).Select(s => s.Label).ToList();
            if (supersetBridges.Count == 0 && adjacencyBridges.Count == 0) continue;

            targets.Add(new ProgressionTarget(keys, cQuality, cRoot, supersetBridges, adjacencyBridges));
        }

        return targets
            .OrderBy(t => t.Root)
            .ThenBy(t => t.Quality)
            .ToList();
    }
}
