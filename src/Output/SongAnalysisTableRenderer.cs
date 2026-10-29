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
        bool showPlacements = false,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("#").RightAligned())
            .AddColumn(new TableColumn("Beats").RightAligned())
            .AddColumn(new TableColumn("Keys"))
            .AddColumn(new TableColumn("n").RightAligned());
        if (showPlacements) table.AddColumn(new TableColumn("Placements"));
        table.AddColumn(new TableColumn("Scales"));

        foreach (var a in analyses)
        {
            var chunk = a.Chunk;
            var keys = string.Join(" ", chunk.Keys);
            var beats = $"{Beat(chunk.StartBeat)}–{Beat(chunk.EndBeat)}";

            var scaleLabels = SongAnalysis.ScaleLabels(a);

            var cells = new List<string>
            {
                chunk.Index.ToString(),
                beats,
                keys,
                chunk.Keys.Count.ToString(),
            };
            if (showPlacements) cells.Add(Cap(SongAnalysis.PlacementLabels(a)));
            cells.Add(Cap(scaleLabels));

            table.AddRow(cells.ToArray());
        }

        var mergeNote = mergeRepeats ? ", repeats merged" : "";
        table.Caption(
            $"analyze midi: {analyses.Count} chunk{(analyses.Count == 1 ? "" : "s")} · " +
            $"{subdivisionsPerQuarter}/quarter grid{mergeNote} · {ktet}-tet\n" +
            $"scales: {NamedScales.Legend()}");
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
        var legend = nameJunctions ? $"\nscales: {NamedScales.Legend()}" : "";
        table.Caption(
            $"chunk bridges: {bridges.Count} pair{(bridges.Count == 1 ? "" : "s")} · " +
            $"subset principle ≥ {minSubsetNotes} note{(minSubsetNotes == 1 ? "" : "s")}{subsetNote}{legend}");
        console.Write(table);
    }

    // A greedy fewest-contiguous-segments view (opt-in, --segments). Each row is a maximal run of
    // consecutive chunks explainable by one shared scale/placement — so the longest row is the
    // longest unbroken chain of a single label, and the row count is the minimum segmentation.
    public static void RenderSegments(
        string title,
        IReadOnlyList<LabelSegment> segments,
        int chunkCount,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("#").RightAligned())
            .AddColumn(new TableColumn("Beats").RightAligned())
            .AddColumn(new TableColumn("Chunks").RightAligned())
            .AddColumn(new TableColumn("Len").RightAligned())
            .AddColumn(new TableColumn("Witnesses"));

        var seg = 0;
        foreach (var s in segments)
        {
            table.AddRow(
                (seg++).ToString(),
                $"{Beat(s.StartBeat)}–{Beat(s.EndBeat)}",
                s.FromIndex == s.ToIndex ? s.FromIndex.ToString() : $"{s.FromIndex}–{s.ToIndex}",
                s.Length.ToString(),
                Cap(s.Witnesses));
        }

        LabelSegment? longest = segments.Count == 0
            ? null
            : segments.Aggregate((a, b) => b.Length > a.Length ? b : a);
        var longestNote = longest is { } l
            ? $" · longest chain {l.Length} chunk{(l.Length == 1 ? "" : "s")} " +
              $"({(l.Witnesses.Count == 0 ? "—" : string.Join(", ", l.Witnesses.Take(MaxItems)))})"
            : "";
        table.Caption(
            $"{title}: {segments.Count} segment{(segments.Count == 1 ? "" : "s")} over " +
            $"{chunkCount} chunk{(chunkCount == 1 ? "" : "s")}{longestNote}");
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
