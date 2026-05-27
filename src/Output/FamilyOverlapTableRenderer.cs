using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class FamilyOverlapTableRenderer
{
    public static void Render(
        int aLcm,
        int bLcm,
        int ktet,
        IReadOnlyList<int> bKeysAtZero,
        IReadOnlyList<FamilyOverlapRow> rows,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Key").RightAligned())
            .AddColumn(new TableColumn("Placement").LeftAligned())
            .AddColumn(new TableColumn("Overlap").LeftAligned());

        foreach (var row in rows)
        {
            var aKeysStr = string.Join(" ", row.AKeys);
            var intersectionStr = string.Join(" ", row.Intersection);
            var isFullOverlap = row.Intersection.Count == row.AKeys.Distinct().Count();
            var cells = new[] { row.At.ToString(), aKeysStr, intersectionStr };
            if (isFullOverlap)
            {
                for (var c = 0; c < cells.Length; c++)
                {
                    cells[c] = $"[green]{cells[c]}[/]";
                }
            }
            table.AddRow(cells);
        }

        var bKeysStr = "{" + string.Join(", ", bKeysAtZero) + "}";
        table.Caption($"family-overlap: A=LCM {aLcm} swept against B=LCM {bLcm} @ 0 = {bKeysStr} · {ktet}-tet");
        console.Write(table);
    }
}
