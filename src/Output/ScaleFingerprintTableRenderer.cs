using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class ScaleFingerprintTableRenderer
{
    public static void Render(
        IReadOnlyCollection<int> chordKeys,
        int ktet,
        IReadOnlyList<FingerprintRow> rows,
        bool nameComposites,
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
            cells[0] = Label(row, ktet, nameComposites);
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
        var caption =
            $"scale-fingerprint: chord={chordStr} · {rows.Count} scale row{(rows.Count == 1 ? "" : "s")} · {ktet}-tet";
        if (nameComposites)
            caption += $"\n{NamedScales.Legend()}";
        table.Caption(caption);
        console.Write(table);
    }

    // Row label. With --name-composites, a row whose key set exactly matches a catalog scale reads as
    // its shorthand "{Short}@{Anchor}" (single placements can name too, e.g. 24@0 → M@0); otherwise the
    // placement constituents — "{Lcm}@{At}" for a single placement, "{PA}·{S}·{PB}" for a composite
    // (the same · form `table composite-scales` prints). The concrete keys are read off the bullet grid.
    private static string Label(FingerprintRow row, int ktet, bool nameComposites)
    {
        if (nameComposites && NamedScales.Match(row.Keys, ktet) is { } m)
            return $"{NamedScales.Shorthand(m.Name)}@{m.Anchor}";

        if (row.S is null || row.PB is null)
            return P(row.PA);
        return $"{P(row.PA)}·{P(row.S.Value)}·{P(row.PB.Value)}";
    }

    private static string P(Placement p) => $"{p.Lcm}@{p.At}";
}
