using System.Globalization;
using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class KeySweepTableRenderer
{
    public static void Render(
        IReadOnlyList<KeySweepRow> rows,
        IReadOnlyList<Fraction> goodFractions,
        IReadOnlyList<double> inputRatios,
        int k,
        double binRadius,
        bool fullMatchesOnly = false,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table();
        table.AddColumn(new TableColumn("n").RightAligned());
        table.AddColumn(new TableColumn("2^(n/k)").RightAligned());
        foreach (var gf in goodFractions)
        {
            table.AddColumn(new TableColumn(gf.ToString()).RightAligned());
        }
        table.AddColumn(new TableColumn("LCM").RightAligned());
        table.AddColumn(new TableColumn("Full?").Centered());

        var fullMatchCount = 0;
        var ambiguousFullCount = 0;
        foreach (var row in rows)
        {
            if (row.FullMatch) fullMatchCount++;
            else if (row.AllInputsBinned && row.Ambiguous) ambiguousFullCount++;

            if (fullMatchesOnly && !row.AllInputsBinned) continue;

            string? colour = row.FullMatch ? "green" : row.Ambiguous ? "yellow" : null;

            var cellStrings = new string[goodFractions.Count + 4];
            cellStrings[0] = row.KeyIndex.ToString(CultureInfo.InvariantCulture);
            cellStrings[1] = row.KeyRatio.ToString("F6", CultureInfo.InvariantCulture);

            for (var c = 0; c < goodFractions.Count; c++)
            {
                cellStrings[c + 2] = OctaveSweepTableRenderer.FormatCell(row.Cells, goodFractions[c]);
            }

            cellStrings[goodFractions.Count + 2] = row.PostBinLcm is int lcm
                ? lcm.ToString(CultureInfo.InvariantCulture) + (row.LcmIsCandidate ? "?" : "")
                : "";
            cellStrings[goodFractions.Count + 3] = row.FullMatch ? "✓" : row.Ambiguous ? "?" : "";

            if (colour is not null)
            {
                for (var c = 0; c < cellStrings.Length; c++)
                {
                    cellStrings[c] = $"[{colour}]{Markup.Escape(cellStrings[c])}[/]";
                }
            }
            else
            {
                for (var c = 0; c < cellStrings.Length; c++)
                {
                    cellStrings[c] = Markup.Escape(cellStrings[c]);
                }
            }

            table.AddRow(cellStrings);
        }

        var ratiosCsv = string.Join(", ", inputRatios.Select(r => r.ToString("F4", CultureInfo.InvariantCulture)));
        var radiusPct = binRadius.ToString("P4", CultureInfo.InvariantCulture);
        var filterClause = fullMatchesOnly ? " (filter active: showing full matches only)" : "";
        table.Caption(
            $"k = {k}-tet; {rows.Count} key{(rows.Count == 1 ? "" : "s")} swept; " +
            $"bin radius = {binRadius.ToString("G4", CultureInfo.InvariantCulture)} ({radiusPct}); " +
            $"{fullMatchCount} full match{(fullMatchCount == 1 ? "" : "es")} " +
            $"({ambiguousFullCount} ambiguous){filterClause}; " +
            $"input ratios = {{{ratiosCsv}}}");

        console.Write(table);
    }
}
