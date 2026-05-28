using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class PlacementTableRenderer
{
    public static void Render(
        IReadOnlyList<(LcmFamily family, Placement placement)> rows,
        int ktet,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("LCM").RightAligned())
            .AddColumn(new TableColumn("At").RightAligned())
            .AddColumn(new TableColumn($"Keys ({ktet}-tet)").LeftAligned())
            .AddColumn(new TableColumn("Fractions").LeftAligned());

        foreach (var (family, placement) in rows)
        {
            var fractionsStr = string.Join(", ", family.Fractions.Select(f => f.ToString()));
            var keysStr = string.Join(" ", placement.Keys);
            table.AddRow(placement.Lcm.ToString(), placement.At.ToString(), keysStr, fractionsStr);
        }

        var at = rows.Count > 0 ? rows[0].placement.At : 0;
        var lcmsStr = string.Join(",", rows.Select(r => r.placement.Lcm));
        table.Caption($"placement: {lcmsStr}@{at} · {ktet}-tet");
        console.Write(table);
    }
}
