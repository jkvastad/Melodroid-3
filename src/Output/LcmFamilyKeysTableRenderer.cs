using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class LcmFamilyKeysTableRenderer
{
    public static void Render(IReadOnlyList<LcmFamilyKeyRow> rows, int k, IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("LCM").RightAligned())
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn(new TableColumn("Fractions").LeftAligned())
            .AddColumn(new TableColumn($"Keys ({k}-tet)").LeftAligned());

        foreach (var row in rows)
        {
            var fractions = string.Join(", ", row.Fractions.Select(f => f.ToString()));
            var keys = string.Join(" ", row.KeyIndices);
            table.AddRow(row.Lcm.ToString(), row.Fractions.Count.ToString(), fractions, keys);
        }

        table.Caption($"{rows.Count} non-empty famil{(rows.Count == 1 ? "y" : "ies")} · {k}-tet");
        console.Write(table);
    }
}
