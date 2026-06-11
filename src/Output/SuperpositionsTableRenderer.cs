using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class SuperpositionsTableRenderer
{
    public static void Render(
        IReadOnlyCollection<int> targetKeys,
        int ktet,
        int minBlockLcm,
        int maxBlockLcm,
        IReadOnlyList<Superposition> rows,
        bool truncated,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var targetStr = "{" + string.Join(", ", targetKeys.OrderBy(k => k)) + "}";
        var blockRange = minBlockLcm == maxBlockLcm ? $"LCM {minBlockLcm}" : $"LCM {minBlockLcm}–{maxBlockLcm}";

        if (rows.Count == 0)
        {
            console.MarkupLine(
                $"[yellow]No superposition of {blockRange} family placements covers {targetStr} in {ktet}-tet.[/]");
            return;
        }

        var target = new HashSet<int>(targetKeys);

        var table = new Table()
            .AddColumn(new TableColumn("#").RightAligned())
            .AddColumn(new TableColumn("Pieces").LeftAligned())
            .AddColumn(new TableColumn($"Union ({ktet}-tet)").LeftAligned())
            .AddColumn(new TableColumn("Extra").LeftAligned())
            .AddColumn(new TableColumn("Disjoint?").Centered());

        foreach (var row in rows)
        {
            var piecesStr = string.Join(" + ", row.Pieces.Select(p => $"{p.Lcm}@{p.At}"));

            var union = new SortedSet<int>(row.Pieces.SelectMany(p => p.Keys));
            var unionStr = string.Join(" ", union.Select(k =>
                target.Contains(k) ? $"[green]{k}[/]" : k.ToString()));

            var extraStr = string.Join(" ", row.ExtraKeys);
            table.AddRow(
                row.Pieces.Count.ToString(),
                piecesStr,
                unionStr,
                extraStr,
                row.DisjointOnTarget ? "●" : "");
        }

        var note = truncated ? " (capped)" : "";
        table.Caption(
            $"superpositions: {targetStr} · {rows.Count} decomposition{(rows.Count == 1 ? "" : "s")}{note} · blocks {blockRange} · {ktet}-tet");
        console.Write(table);
    }
}
