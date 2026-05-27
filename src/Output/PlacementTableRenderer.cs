using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class PlacementTableRenderer
{
    public static void Render(
        Placement placement,
        IReadOnlyList<Fraction> fractions,
        int ktet,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("LCM").RightAligned())
            .AddColumn(new TableColumn("At").RightAligned())
            .AddColumn(new TableColumn("Fractions").LeftAligned())
            .AddColumn(new TableColumn($"Keys ({ktet}-tet)").LeftAligned());

        var fractionsStr = string.Join(", ", fractions.Select(f => f.ToString()));
        var keysStr = string.Join(" ", placement.Keys);
        table.AddRow(placement.Lcm.ToString(), placement.At.ToString(), fractionsStr, keysStr);

        table.Caption($"placement: {placement.Lcm}@{placement.At} · {ktet}-tet");
        console.Write(table);
    }
}
