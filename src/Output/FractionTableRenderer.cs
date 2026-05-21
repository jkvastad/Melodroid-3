using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class FractionTableRenderer
{
    public static void Render(IReadOnlyList<Fraction> fractions, IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Fraction").RightAligned())
            .AddColumn(new TableColumn("Decimal").RightAligned());

        foreach (var f in fractions)
        {
            table.AddRow(f.ToString(), f.Value.ToString("F3"));
        }

        table.Caption($"{fractions.Count} fraction{(fractions.Count == 1 ? "" : "s")}");
        console.Write(table);
    }
}
