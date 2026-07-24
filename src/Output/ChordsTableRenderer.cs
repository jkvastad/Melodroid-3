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
        bool allowDim = false,
        bool lcmOnly = false,
        int maxLcm = 0,
        IAnsiConsole? console = null)
    {
        var filters = new List<string>();
        if (lcmOnly) filters.Add($"lcm-only (max-lcm {maxLcm})");
        if (noMinorSeconds) filters.Add("no minor seconds" + (allowMajorSevenths ? " (maj7 allowed)" : ""));
        if (noTritones) filters.Add("no tritones" + (allowDim ? " (dim allowed)" : ""));
        var filterClause = filters.Count > 0 ? " · " + string.Join(" · ", filters) : "";
        // With --lcm-only every row is a subset of some family; otherwise the count is the raw
        // necklace total for the range.
        var countLabel = lcmOnly
            ? $"{chords.Count} subset{(chords.Count == 1 ? "" : "s")} of ≥1 family"
            : $"{chords.Count} unique under transposition";
        var caption =
            $"chords: {ktet}-tet · {SizeLabel(minNotes, maxNotes)}{filterClause} · " +
            countLabel +
            Breakdown(chords) +
            TruncClause(truncated);

        Render(chords, placements, caption, console);
    }

    private static void Render(
        IReadOnlyList<Chord> chords,
        IReadOnlyList<IReadOnlyList<KeySupersetRow>> placements,
        string caption,
        IAnsiConsole? console)
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

        table.Caption(caption);
        console.Write(table);
    }

    private static string SizeLabel(int minNotes, int maxNotes) =>
        minNotes == maxNotes ? $"size {minNotes}" : $"sizes {minNotes}–{maxNotes}";

    // Per-size breakdown wrapped in parens, e.g. " (6 · 19 · 43)" across the sizes present, or "".
    private static string Breakdown(IReadOnlyList<Chord> chords)
    {
        var perSize = string.Join(" · ", chords
            .GroupBy(c => c.Keys.Count)
            .OrderBy(g => g.Key)
            .Select(g => g.Count().ToString(CultureInfo.InvariantCulture)));
        return perSize.Length > 0 ? $" ({perSize})" : "";
    }

    private static string TruncClause(bool truncated) =>
        truncated ? " [red](truncated by --max-results)[/]" : "";

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
