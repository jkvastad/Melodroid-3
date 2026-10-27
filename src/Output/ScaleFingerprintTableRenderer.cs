using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class ScaleFingerprintTableRenderer
{
    public static void Render(
        IReadOnlyCollection<int> chordKeys,
        int ktet,
        IReadOnlyList<ScaleRow> rows,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var chordSet = chordKeys.ToHashSet();

        var table = new Table()
            .AddColumn(new TableColumn("Scale").RightAligned());
        for (var k = 0; k < ktet; k++)
        {
            var header = chordSet.Contains(k) ? $"[green]{k}[/]" : k.ToString();
            table.AddColumn(new TableColumn(header).Centered());
        }

        foreach (var row in rows)
        {
            var keySet = new HashSet<int>(row.Keys);
            var cells = new string[ktet + 1];
            cells[0] = Label(row);
            // Chord keys read green, every other scale member a plain bullet. (A per-column
            // "distinguishing key" highlight — marking member keys unique to this row within a
            // column — would sharpen the implied-scale reading but needs cross-row state; left as a
            // possible follow-up.)
            for (var k = 0; k < ktet; k++)
            {
                if (!keySet.Contains(k))
                    cells[k + 1] = string.Empty;
                else
                    cells[k + 1] = chordSet.Contains(k) ? "[green]●[/]" : "●";
            }
            table.AddRow(cells);
        }

        var chordStr = "{" + string.Join(", ", chordKeys.OrderBy(k => k)) + "}";
        table.Caption(
            $"scale-fingerprint: chord={chordStr} · {rows.Count} scale row{(rows.Count == 1 ? "" : "s")} · " +
            $"{NamedScales.Catalog.Count} scales · {ktet}-tet");
        console.Write(table);
    }

    // Row label: "{Name} @{Anchor}", e.g. "harmonic minor @9". The concrete keys are read off the
    // bullet grid, so the label stays on one line.
    private static string Label(ScaleRow row) =>
        $"{Markup.Escape(row.Name)} @{row.Anchor}";
}
