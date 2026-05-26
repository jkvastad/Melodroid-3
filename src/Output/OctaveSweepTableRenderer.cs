using System.Globalization;
using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class OctaveSweepTableRenderer
{
    public static void Render(
        IReadOnlyList<OctaveSweepRow> rows,
        IReadOnlyList<Fraction> goodFractions,
        IReadOnlyList<double> inputRatios,
        double binRadius,
        bool fullMatchesOnly = false,
        bool centeredFullMatchesOnly = false,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var centered = OctaveSweep.IdentifyCenteredFullMatches(rows);

        var table = new Table();
        table.AddColumn(new TableColumn("Ref").RightAligned());
        foreach (var gf in goodFractions)
        {
            table.AddColumn(new TableColumn(gf.ToString()).RightAligned());
        }
        table.AddColumn(new TableColumn("LCM").RightAligned());
        table.AddColumn(new TableColumn("Full?").Centered());

        var fullMatchCount = 0;
        var ambiguousFullCount = 0;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.FullMatch) fullMatchCount++;
            else if (row.AllInputsBinned && row.Ambiguous) ambiguousFullCount++;

            if (centeredFullMatchesOnly)
            {
                if (!centered.Contains(rowIndex)) continue;
            }
            else if (fullMatchesOnly && !row.AllInputsBinned)
            {
                continue;
            }

            var isCentered = centered.Contains(rowIndex);
            string? colour = row.FullMatch ? "green" : row.Ambiguous ? "yellow" : null;

            var cellStrings = new string[goodFractions.Count + 3];
            cellStrings[0] = row.ReferenceRatio.ToString("F4", CultureInfo.InvariantCulture);

            for (var c = 0; c < goodFractions.Count; c++)
            {
                cellStrings[c + 1] = FormatCell(row.Cells, goodFractions[c]);
            }

            cellStrings[goodFractions.Count + 1] = row.PostBinLcm is int lcm
                ? lcm.ToString(CultureInfo.InvariantCulture) + (row.LcmIsCandidate ? "?" : "")
                : "";
            cellStrings[goodFractions.Count + 2] = isCentered ? "★" : row.FullMatch ? "✓" : row.Ambiguous ? "?" : "";

            if (colour is not null)
            {
                for (var c = 0; c < cellStrings.Length; c++)
                {
                    var text = Markup.Escape(cellStrings[c]);
                    cellStrings[c] = $"[{colour}]{text}[/]";
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
        var radiusPct = binRadius.ToString("P3", CultureInfo.InvariantCulture);
        var filterClause = centeredFullMatchesOnly
            ? " (filter active: centered full matches only)"
            : fullMatchesOnly
                ? " (filter active: showing full matches only)"
                : "";
        var fullMatchesClause =
            $"{fullMatchCount} full match{(fullMatchCount == 1 ? "" : "es")} " +
            $"({centered.Count} centered, {ambiguousFullCount} ambiguous)" +
            filterClause;
        table.Caption(
            $"{rows.Count} reference{(rows.Count == 1 ? "" : "s")} swept; " +
            $"bin radius = {binRadius.ToString("G4", CultureInfo.InvariantCulture)} ({radiusPct}); " +
            $"{fullMatchesClause}; " +
            $"input ratios = {{{ratiosCsv}}}");

        console.Write(table);
    }

    internal static string FormatCell(IReadOnlyList<OctaveSweepCell> cells, Fraction column)
    {
        foreach (var cell in cells)
        {
            foreach (var match in cell.Matches)
            {
                if (match.Fraction == column)
                {
                    var distance = match.SignedPctDistance.ToString(
                        "+0.000\\ \\%;-0.000\\ \\%;0.000\\ \\%",
                        CultureInfo.InvariantCulture);
                    return cell.Ambiguous ? "~" + distance : distance;
                }
            }
        }
        return "";
    }
}
