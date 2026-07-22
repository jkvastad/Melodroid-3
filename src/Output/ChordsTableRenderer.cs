using System.Globalization;
using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class ChordsTableRenderer
{
    // Cap on how many placement tokens a single chord row lists before overflowing to " …(+M)".
    private const int MaxPlacementsShown = 8;

    public static void Render(
        IReadOnlyList<Chord> chords,
        IReadOnlyList<IReadOnlyList<KeySupersetRow>> placements,
        int ktet,
        int minNotes,
        int maxNotes,
        bool truncated,
        bool noMinorSeconds = false,
        bool allowMajorSevenths = false,
        bool noTritones = false,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table();
        table.AddColumn(new TableColumn("Size").RightAligned());
        table.AddColumn(new TableColumn("Keys").LeftAligned());
        table.AddColumn(new TableColumn("Orbit").RightAligned());
        table.AddColumn(new TableColumn("Placements").LeftAligned());

        for (var i = 0; i < chords.Count; i++)
        {
            var chord = chords[i];
            table.AddRow(
                chord.Keys.Count.ToString(CultureInfo.InvariantCulture),
                string.Join(" ", chord.Keys),
                chord.OrbitSize.ToString(CultureInfo.InvariantCulture),
                FormatPlacements(placements[i]));
        }

        // Per-size breakdown, e.g. "6 · 19 · 43" across the sizes actually present.
        var perSize = chords
            .GroupBy(c => c.Keys.Count)
            .OrderBy(g => g.Key)
            .Select(g => g.Count().ToString(CultureInfo.InvariantCulture));
        var breakdown = string.Join(" · ", perSize);
        var sizeLabel = minNotes == maxNotes ? $"size {minNotes}" : $"sizes {minNotes}–{maxNotes}";
        var filters = new List<string>();
        if (noMinorSeconds) filters.Add("no minor seconds" + (allowMajorSevenths ? " (maj7 allowed)" : ""));
        if (noTritones) filters.Add("no tritones");
        var filterClause = filters.Count > 0 ? " · " + string.Join(" · ", filters) : "";
        var truncClause = truncated ? " [red](truncated by --max-results)[/]" : "";
        table.Caption(
            $"chords: {ktet}-tet · {sizeLabel}{filterClause} · " +
            $"{chords.Count} unique under transposition" +
            (breakdown.Length > 0 ? $" ({breakdown})" : "") +
            truncClause);

        console.Write(table);
    }

    // The containing LCM-family placements for one chord, tightest first (FindSupersets already
    // sorts by extra-keys count, then LCM, then anchor). Each token is "lcm@at" — the family LCM at
    // the anchor key where its 1/1 fundamental sits; any keys the placement carries beyond the chord
    // are left implicit (inspect them via `key-supersets`). Empty for a chord that no family
    // placement contains; overflow past the cap collapses to " …(+M)".
    private static string FormatPlacements(IReadOnlyList<KeySupersetRow> rows)
    {
        if (rows.Count == 0) return "";

        var tokens = rows.Take(MaxPlacementsShown).Select(r => $"{r.Placement.Lcm}@{r.Placement.At}");
        var text = string.Join(" ", tokens);
        if (rows.Count > MaxPlacementsShown) text += $" …(+{rows.Count - MaxPlacementsShown})";
        return text;
    }
}
