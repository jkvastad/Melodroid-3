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
        bool uniqueReference,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var targetStr = "{" + string.Join(", ", targetKeys.OrderBy(k => k)) + "}";
        var blockRange = minBlockLcm == maxBlockLcm ? $"LCM {minBlockLcm}" : $"LCM {minBlockLcm}–{maxBlockLcm}";
        var mode = uniqueReference ? "unique-reference" : "any-reference";

        if (rows.Count == 0)
        {
            var refNote = uniqueReference ? "unique-reference " : "";
            console.MarkupLine(
                $"[yellow]No {refNote}superposition of {blockRange} family placements covers {targetStr} in {ktet}-tet.[/]");
            return;
        }

        var target = new HashSet<int>(targetKeys);

        var table = new Table()
            .AddColumn(new TableColumn("#").RightAligned());
        if (uniqueReference)
        {
            table.AddColumn(new TableColumn("Ref").RightAligned());
            table.AddColumn(new TableColumn("LCM").RightAligned());
        }
        table
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
            var cells = new List<string> { row.Pieces.Count.ToString() };
            if (uniqueReference)
            {
                cells.Add(row.Reference?.ToString() ?? "");
                cells.Add(row.CombinedLcm?.ToString() ?? "");
            }
            cells.Add(piecesStr);
            cells.Add(unionStr);
            cells.Add(extraStr);
            cells.Add(row.DisjointOnTarget ? "●" : "");
            table.AddRow(cells.ToArray());
        }

        var note = truncated ? " (capped)" : "";
        table.Caption(
            $"superpositions ({mode}): {targetStr} · {rows.Count} decomposition{(rows.Count == 1 ? "" : "s")}{note} · blocks {blockRange} · {ktet}-tet");
        console.Write(table);
    }
}
