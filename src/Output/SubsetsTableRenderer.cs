using System.Globalization;
using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class SubsetsTableRenderer
{
    public static void Render(
        string inputLabel,
        IReadOnlyList<int> baseKeys,
        IReadOnlyList<SubsetMatch> matches,
        int k,
        double binRadius,
        bool strictOnly,
        bool truncated,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        // One row per subset; one column per reference key n (0..k-1). Cells hold the LCM the
        // subset full-matches at that reference (green strict, yellow ambiguous with a trailing ?).
        var table = new Table();
        table.AddColumn(new TableColumn("Subset \\ ref n").LeftAligned());
        for (var n = 0; n < k; n++)
        {
            table.AddColumn(new TableColumn(n.ToString(CultureInfo.InvariantCulture)).RightAligned());
        }

        var strictCount = 0;
        var ambiguousCount = 0;
        var rowCount = 0;
        foreach (var group in matches.GroupBy(mm => string.Join(" ", mm.Keys)))
        {
            rowCount++;
            var byRef = new SubsetMatch?[k];
            foreach (var mm in group)
            {
                byRef[mm.Reference] = mm;
                if (mm.Strict) strictCount++; else ambiguousCount++;
            }

            var cells = new string[k + 1];
            cells[0] = Markup.Escape(group.Key);
            for (var n = 0; n < k; n++)
            {
                if (byRef[n] is SubsetMatch hit)
                {
                    var colour = hit.Strict ? "green" : "yellow";
                    var text = hit.Lcm.ToString(CultureInfo.InvariantCulture) + (hit.Strict ? "" : "?");
                    cells[n + 1] = $"[{colour}]{Markup.Escape(text)}[/]";
                }
                else
                {
                    cells[n + 1] = "";
                }
            }
            table.AddRow(cells);
        }

        var radiusPct = binRadius.ToString("P4", CultureInfo.InvariantCulture);
        var keysCsv = string.Join(" ", baseKeys);
        var filterClause = strictOnly ? " (strict full matches only)" : "";
        var truncClause = truncated ? " [red](truncated by --max-results)[/]" : "";
        table.Caption(
            $"subsets of {inputLabel}; base keys = {{{keysCsv}}}; k = {k}-tet; " +
            $"bin radius = {binRadius.ToString("G4", CultureInfo.InvariantCulture)} ({radiusPct}); " +
            $"{rowCount} subset{(rowCount == 1 ? "" : "s")}, {matches.Count} match{(matches.Count == 1 ? "" : "es")} " +
            $"({strictCount} strict, {ambiguousCount} ambiguous){filterClause}{truncClause}");

        console.Write(table);
    }
}
