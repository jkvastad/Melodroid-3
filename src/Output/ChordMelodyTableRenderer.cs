using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class ChordMelodyTableRenderer
{
    public static void Render(
        IReadOnlyCollection<int> chordKeys,
        int ktet,
        IReadOnlyList<ChordMelodyRow> rows,
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
            $"chord-melody: chord={chordStr} · {rows.Count} scale row{(rows.Count == 1 ? "" : "s")} · {ktet}-tet";
        if (nameComposites)
            caption += $"\n{NamedScales.Legend()}";
        table.Caption(caption);
        console.Write(table);
    }

    // Row label. Single placements always read "{Lcm}@{At}" — a pure LCM placement is never renamed
    // (so the major-scale placement stays 24@0, not M@0). A composite reads "{PA}·{S}·{PB}", or, with
    // --name-composites, its shorthand "{Short}@{Anchor}" when its key set matches a catalog scale
    // (the same · form `table composite-scales` prints). The concrete keys are read off the bullet grid.
    private static string Label(ChordMelodyRow row, int ktet, bool nameComposites)
    {
        if (row.S is not { } s || row.PB is not { } pb)
            return P(row.PA);   // single placement: always {Lcm}@{At}

        if (nameComposites && NamedScales.Match(row.Keys, ktet) is { } m)
            return $"{NamedScales.Shorthand(m.Name)}@{m.Anchor}";
        return $"{P(row.PA)}·{P(s)}·{P(pb)}";
    }

    private static string P(Placement p) => $"{p.Lcm}@{p.At}";
}
