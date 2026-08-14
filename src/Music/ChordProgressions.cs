namespace Melodroid_3.Music;

// Perception-based chord progression logic: which major/minor/dim triads a given triad may
// progress to. Ported from the website's perception walk (website/src/lib/perceptionTables.ts)
// and documented in website/docs/music/voicings-and-lcm-families.mdx ("Perception Based
// Progression"). Unlike the placement math this is *authored* data (subjective perception), so
// there is no oracle — the per-quality stable-superset lists and the 15s key set are transcribed
// from the doc and must be kept consistent with perceptionTables.ts.
//
// Only the `stableSupersets` lists plus the adjacency derivation matter for progression; the
// opening-key `entries` in the TS tables only pick a melody and are not ported here. This command
// is inherently 12-tet — the tables and the 15s set are defined mod 12.

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

    // A superset reference in a perception table, relative to the chord root. Only the lcm-24
    // family and the named 15s set appear across the three tables' stableSupersets.
    private enum RefKind { Lcm24, Fifteens }
    private readonly record struct SupersetRef(RefKind Kind, int At);

    // Root-relative triad shapes (folded pitch classes).
    private static readonly IReadOnlyList<int> MajorShape = new[] { 0, 4, 7 };
    private static readonly IReadOnlyList<int> MinorShape = new[] { 0, 3, 7 };
    private static readonly IReadOnlyList<int> DimShape = new[] { 0, 3, 6 };

    // The 15s (stable-15 subset) base key set at anchor 0 — the only named label any
    // stableSupersets list references (blues/harm are used only by the melody entries).
    private static readonly int[] FifteensBase = { 0, 1, 3, 5, 9, 10 };

    // Per-quality stable melodic supersets, root-relative (perceptionTables.ts:92,114,140).
    private static readonly IReadOnlyList<SupersetRef> MajorSupersets = new[]
    {
        new SupersetRef(RefKind.Lcm24, 0),
        new SupersetRef(RefKind.Lcm24, 5),
        new SupersetRef(RefKind.Lcm24, 7),
        new SupersetRef(RefKind.Fifteens, 7),
    };

    private static readonly IReadOnlyList<SupersetRef> MinorSupersets = new[]
    {
        new SupersetRef(RefKind.Lcm24, 3),
        new SupersetRef(RefKind.Lcm24, 8),
        new SupersetRef(RefKind.Lcm24, 10),
        new SupersetRef(RefKind.Fifteens, 2),
    };

    private static readonly IReadOnlyList<SupersetRef> DimSupersets = new[]
    {
        new SupersetRef(RefKind.Fifteens, 3),
        new SupersetRef(RefKind.Lcm24, 1),
    };

    private static IReadOnlyList<int> Shape(TriadQuality quality) => quality switch
    {
        TriadQuality.Major => MajorShape,
        TriadQuality.Minor => MinorShape,
        _ => DimShape,
    };

    private static IReadOnlyList<SupersetRef> StableSupersets(TriadQuality quality) => quality switch
    {
        TriadQuality.Major => MajorSupersets,
        TriadQuality.Minor => MinorSupersets,
        _ => DimSupersets,
    };

    private static int Fold(int key) => ((key % Ktet) + Ktet) % Ktet;

    private static IReadOnlyList<int> FoldSet(IEnumerable<int> keys) =>
        keys.Select(Fold).Distinct().OrderBy(k => k).ToList();

    // Adjacency rule (perceptionTables.ts:156-165): each 15s@X in the stable supersets yields the
    // adjacent lcm-24 placements 24@(X+1) and 24@(X+8) (mod 12).
    private static IReadOnlyList<SupersetRef> AdjacencySupersets(TriadQuality quality) =>
        StableSupersets(quality)
            .Where(s => s.Kind == RefKind.Fifteens)
            .SelectMany(s => new[]
            {
                new SupersetRef(RefKind.Lcm24, Fold(s.At + 1)),
                new SupersetRef(RefKind.Lcm24, Fold(s.At + 8)),
            })
            .ToList();

    // Resolve a root-relative ref to absolute folded k-tet keys. Lcm refs reuse Placements.Compute
    // (the placement math); the 15s ref rotates its base set. Matches TS resolvePlacementKeys.
    private static IReadOnlyList<int> Resolve(SupersetRef reference, int root, LcmFamily lcm24Family)
    {
        var at = Fold(reference.At + root);
        return reference.Kind == RefKind.Lcm24
            ? FoldSet(Placements.Compute(lcm24Family, at, Ktet).Keys)
            : FoldSet(FifteensBase.Select(k => k + at));
    }

    private static string Label(SupersetRef reference, int root)
    {
        var at = Fold(reference.At + root);
        return reference.Kind == RefKind.Lcm24 ? $"24@{at}" : $"15s@{at}";
    }

    // Match a folded key set to (quality, root); null when it is not a major/minor/dim triad.
    public static (TriadQuality Quality, int Root)? Identify(IReadOnlyCollection<int> keys)
    {
        var target = FoldSet(keys);
        var id = string.Join(",", target);
        foreach (var quality in new[] { TriadQuality.Major, TriadQuality.Minor, TriadQuality.Diminished })
        {
            var shape = Shape(quality);
            for (var root = 0; root < Ktet; root++)
            {
                var folded = FoldSet(shape.Select(k => k + root));
                if (string.Join(",", folded) == id) return (quality, root);
            }
        }
        return null;
    }

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

    // Every next-chord target for the chord (quality at root): each triad that is a subset of a
    // resolved superset under an included rule. Both bridge lists are populated; a target is
    // included when at least one included rule reaches it. Sorted by root, then quality.
    public static IReadOnlyList<ProgressionTarget> Compute(
        TriadQuality quality, int root,
        bool includeSupersets, bool includeAdjacency,
        LcmFamily lcm24Family)
    {
        var supersetRefs = includeSupersets ? StableSupersets(quality) : Array.Empty<SupersetRef>();
        var adjacencyRefs = includeAdjacency ? AdjacencySupersets(quality) : Array.Empty<SupersetRef>();

        var supersets = supersetRefs
            .Select(r => (Keys: new HashSet<int>(Resolve(r, root, lcm24Family)), Label: Label(r, root)))
            .ToList();
        var adjacency = adjacencyRefs
            .Select(r => (Keys: new HashSet<int>(Resolve(r, root, lcm24Family)), Label: Label(r, root)))
            .ToList();

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
