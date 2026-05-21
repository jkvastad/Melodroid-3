using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class LcmFamilyTableRenderer
{
    public static void Render(IReadOnlyList<LcmFamily> families, IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("LCM").RightAligned())
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn(new TableColumn("Fractions").LeftAligned());

        foreach (var family in families)
        {
            var fractions = string.Join(", ", family.Fractions.Select(f => f.ToString()));
            table.AddRow(family.Lcm.ToString(), family.Fractions.Count.ToString(), fractions);
        }

        table.Caption($"{families.Count} non-empty famil{(families.Count == 1 ? "y" : "ies")}");
        console.Write(table);
    }
}
