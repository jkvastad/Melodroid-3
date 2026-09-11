using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class RenormalizationTableRenderer
{
    public static void Render(
        LcmFamily family,
        IReadOnlyList<(Fraction Base, IReadOnlyList<Fraction> Fractions)> rows,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Base").RightAligned())
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn(new TableColumn("Fractions").LeftAligned());

        foreach (var (baseFrac, fractions) in rows)
        {
            var joined = string.Join(", ", fractions.Select(f => f.ToString()));
            table.AddRow(baseFrac.ToString(), fractions.Count.ToString(), joined);
        }

        table.Caption($"lcm-{family.Lcm} · {family.Fractions.Count} members");
        console.Write(table);
    }
}
