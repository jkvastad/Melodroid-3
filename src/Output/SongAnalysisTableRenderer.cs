using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

// Per-chunk timeline for `analyze midi`: one row per discretized chunk, listing the sounding
// pitch-class key-set and every derived LCM-family placement / named scale that contains it. A
// chunk's harmonic identity is its key-set plus these containments — no traditional chord names, per
// the "first principles, not tradition" anchor. Placement and scale cells are capped for readability.
public static class SongAnalysisTableRenderer
{
    private const int MaxItems = 8;

    public static void Render(
        IReadOnlyList<ChunkAnalysis> analyses,
        int ktet,
        int subdivisionsPerQuarter,
        bool mergeRepeats,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("#").RightAligned())
            .AddColumn(new TableColumn("Beats").RightAligned())
            .AddColumn(new TableColumn("Keys"))
            .AddColumn(new TableColumn("n").RightAligned())
            .AddColumn(new TableColumn("Placements"))
            .AddColumn(new TableColumn("Scales"));

        foreach (var a in analyses)
        {
            var chunk = a.Chunk;
            var keys = string.Join(" ", chunk.Keys);
            var beats = $"{Beat(chunk.StartBeat)}–{Beat(chunk.EndBeat)}";

            var placementLabels = Placements.CollapseIsomorphic(a.Placements)
                .Select(r => $"{r.Placement.Lcm}@{r.Placement.At}")
                .ToList();
            var scaleLabels = a.Scales
                .Select(s => $"{s.Name}@{s.Anchor}")
                .ToList();

            table.AddRow(
                chunk.Index.ToString(),
                beats,
                keys,
                chunk.Keys.Count.ToString(),
                Cap(placementLabels),
                Cap(scaleLabels));
        }

        var mergeNote = mergeRepeats ? ", repeats merged" : "";
        table.Caption(
            $"analyze midi: {analyses.Count} chunk{(analyses.Count == 1 ? "" : "s")} · " +
            $"{subdivisionsPerQuarter}/quarter grid{mergeNote} · {ktet}-tet");
        console.Write(table);
    }

    // The cross-chunk progression bridges (opt-in, --bridges). One row per consecutive chunk pair;
    // reading the Superset column top-to-bottom surfaces the progression loop a per-chunk containment
    // list hides (e.g. 8@5, 24@0, 8@0 for a C→F→G→C loop).
    public static void RenderBridges(
        IReadOnlyList<ChunkBridge> bridges,
        int minSubsetNotes,
        bool nameJunctions = false,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Pair").RightAligned())
            .AddColumn(new TableColumn("Move"))
            .AddColumn(new TableColumn("Category"))
            .AddColumn(new TableColumn("Superset"))
            .AddColumn(new TableColumn(nameJunctions ? "Junction" : "Subset"));

        foreach (var b in bridges)
        {
            var move = $"{string.Join(" ", b.FromKeys)} → {string.Join(" ", b.ToKeys)}";
            table.AddRow(
                $"{b.FromIndex}→{b.ToIndex}",
                move,
                b.Category.ToString(),
                Cap(b.SupersetBridges),
                Cap(b.SubsetBridges));
        }

        var subsetNote = nameJunctions ? ", junctions named" : ", raw triplets";
        table.Caption(
            $"chunk bridges: {bridges.Count} pair{(bridges.Count == 1 ? "" : "s")} · " +
            $"subset principle ≥ {minSubsetNotes} note{(minSubsetNotes == 1 ? "" : "s")}{subsetNote}");
        console.Write(table);
    }

    private static string Beat(double b) => b.ToString("0.##");

    private static string Cap(IReadOnlyList<string> items)
    {
        if (items.Count == 0) return "[grey]—[/]";
        if (items.Count <= MaxItems) return string.Join(", ", items);
        return string.Join(", ", items.Take(MaxItems)) + $" …(+{items.Count - MaxItems})";
    }
}
