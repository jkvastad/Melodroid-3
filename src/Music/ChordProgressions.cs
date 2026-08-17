namespace Melodroid_3.Music;

// Perception-based chord progression logic: which major/minor/dim triads a given chord (any set of
// 12-tet keys) may progress to. The stable melodic supersets of the source chord are *derived* from
// the placement math — exactly the rows of
//   chord-melody --drop-renormalized-subsets --drop-collapsed --stable-15
// (see Placements.FindMaximalContaining / DropCollapsed / StableFifteen). Adjacency then follows
// from any stable lcm-15 row. This mirrors the "Perception Based Progression" derivation in
// website/docs/music/voicings-and-lcm-families.mdx. The command is inherently 12-tet — the 15s
// stabilisation and the adjacency rule are defined mod 12.

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
    // of one of the chord's stable melodic supersets (the superset rule) or of an adjacency-derived
    // lcm-24 placement (the adjacency rule). Both bridge lists are populated; a target is included
    // when at least one included rule reaches it. Sorted by root, then quality.
    public static IReadOnlyList<ProgressionTarget> Compute(
        IReadOnlyCollection<int> chordKeys,
        bool includeSupersets, bool includeAdjacency,
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<FamilyRelation> relations,
        LcmFamily lcm24Family)
    {
        // Stable melodic supersets = maximal placements containing the chord, with collapsed lcm-15
        // rows dropped and the rest reduced to stable 15s form. Mirrors the chord-melody flags
        // --drop-renormalized-subsets --drop-collapsed --stable-15.
        var stable = Placements.FindMaximalContaining(chordKeys, families, relations, Ktet, dropRenormalizedSubsets: true);
        stable = Placements.DropCollapsed(stable, chordKeys, Ktet);
        stable = Placements.StableFifteen(stable, Ktet);

        var supersets = includeSupersets
            ? stable
                .Select(p => (Keys: new HashSet<int>(FoldSet(p.Keys)), Label: PlacementLabel(p)))
                .ToList()
            : new List<(HashSet<int> Keys, string Label)>();

        // Adjacency rule: each surviving stable lcm-15 row 15s@At reaches the adjacent lcm-24
        // placements 24@(At+1) and 24@(At+8) (mod 12).
        var adjacency = includeAdjacency
            ? stable
                .Where(p => p.Lcm == 15)
                .SelectMany(p => new[] { Fold(p.At + 1), Fold(p.At + 8) })
                .Distinct()
                .Select(at => (Keys: new HashSet<int>(FoldSet(Placements.Compute(lcm24Family, at, Ktet).Keys)), Label: $"24@{at}"))
                .ToList()
            : new List<(HashSet<int> Keys, string Label)>();

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
