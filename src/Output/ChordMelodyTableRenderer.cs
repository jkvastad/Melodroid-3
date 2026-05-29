using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class ChordMelodyTableRenderer
{
    public static void Render(
        IReadOnlyCollection<int> chordKeys,
        IReadOnlyCollection<int> maximalLcms,
        int ktet,
        IReadOnlyList<Placement> placements,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var chordSet = chordKeys.ToHashSet();

        var table = new Table()
            .AddColumn(new TableColumn("Placement").RightAligned());
        for (var k = 0; k < ktet; k++)
        {
            var header = chordSet.Contains(k) ? $"[green]{k}[/]" : k.ToString();
            table.AddColumn(new TableColumn(header).Centered());
        }

        foreach (var placement in placements)
        {
            var keySet = new HashSet<int>(placement.Keys);
            var cells = new string[ktet + 1];
            cells[0] = $"{placement.Lcm}@{placement.At}";
            for (var k = 0; k < ktet; k++)
            {
                if (!keySet.Contains(k))
                {
                    cells[k + 1] = string.Empty;
                }
                else
                {
                    cells[k + 1] = chordSet.Contains(k) ? "[green]●[/]" : "●";
                }
            }
            table.AddRow(cells);
        }

        var chordStr = "{" + string.Join(", ", chordKeys.OrderBy(k => k)) + "}";
        var maxStr = "{" + string.Join(", ", maximalLcms.OrderBy(l => l)) + "}";
        table.Caption($"chord-melody: chord={chordStr} · maximal LCMs: {maxStr} · {placements.Count} placement{(placements.Count == 1 ? "" : "s")} · {ktet}-tet");
        console.Write(table);
    }
}
