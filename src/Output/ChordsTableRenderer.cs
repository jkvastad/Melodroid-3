using System.Globalization;
using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class ChordsTableRenderer
{
    public static void Render(
        IReadOnlyList<Chord> chords,
        int ktet,
        int minNotes,
        int maxNotes,
        bool truncated,
        bool noMinorSeconds = false,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table();
        table.AddColumn(new TableColumn("Size").RightAligned());
        table.AddColumn(new TableColumn("Keys").LeftAligned());
        table.AddColumn(new TableColumn("Intervals").LeftAligned());
        table.AddColumn(new TableColumn("Orbit").RightAligned());

        foreach (var chord in chords)
        {
            table.AddRow(
                chord.Keys.Count.ToString(CultureInfo.InvariantCulture),
                string.Join(" ", chord.Keys),
                string.Join(" ", chord.Intervals),
                chord.OrbitSize.ToString(CultureInfo.InvariantCulture));
        }

        // Per-size breakdown, e.g. "6 · 19 · 43" across the sizes actually present.
        var perSize = chords
            .GroupBy(c => c.Keys.Count)
            .OrderBy(g => g.Key)
            .Select(g => g.Count().ToString(CultureInfo.InvariantCulture));
        var breakdown = string.Join(" · ", perSize);
        var sizeLabel = minNotes == maxNotes ? $"size {minNotes}" : $"sizes {minNotes}–{maxNotes}";
        var filterClause = noMinorSeconds ? " · no minor seconds" : "";
        var truncClause = truncated ? " [red](truncated by --max-results)[/]" : "";
        table.Caption(
            $"chords: {ktet}-tet · {sizeLabel}{filterClause} · " +
            $"{chords.Count} unique under transposition" +
            (breakdown.Length > 0 ? $" ({breakdown})" : "") +
            truncClause);

        console.Write(table);
    }
}
