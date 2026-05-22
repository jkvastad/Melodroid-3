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
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table();
        table.AddColumn(new TableColumn("Ref").RightAligned());
        foreach (var gf in goodFractions)
        {
            table.AddColumn(new TableColumn(gf.ToString()).RightAligned());
        }
        table.AddColumn(new TableColumn("LCM").RightAligned());
        table.AddColumn(new TableColumn("Full?").Centered());

        var fullMatchCount = 0;
        foreach (var row in rows)
        {
            if (row.FullMatch) fullMatchCount++;

            string? colour = row.FullMatch ? "green" : row.Ambiguous ? "yellow" : null;

            var cellStrings = new string[goodFractions.Count + 3];
            cellStrings[0] = row.ReferenceRatio.ToString("F4", CultureInfo.InvariantCulture);

            for (var c = 0; c < goodFractions.Count; c++)
            {
                cellStrings[c + 1] = FormatCell(row, goodFractions[c]);
            }

            cellStrings[goodFractions.Count + 1] = row.PostBinLcm?.ToString(CultureInfo.InvariantCulture) ?? "";
            cellStrings[goodFractions.Count + 2] = row.FullMatch ? "✓" : row.Ambiguous ? "?" : "";

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
        table.Caption(
            $"{rows.Count} reference{(rows.Count == 1 ? "" : "s")} swept; " +
            $"bin radius = {binRadius.ToString("G4", CultureInfo.InvariantCulture)} ({radiusPct}); " +
            $"{fullMatchCount} full match{(fullMatchCount == 1 ? "" : "es")}; " +
            $"input ratios = {{{ratiosCsv}}}");

        console.Write(table);
    }

    private static string FormatCell(OctaveSweepRow row, Fraction column)
    {
        foreach (var cell in row.Cells)
        {
            if (cell.GoodFraction == column && !double.IsNaN(cell.SignedPctDistance))
            {
                var distance = cell.SignedPctDistance.ToString(
                    "+0.000\\ %;-0.000\\ %;0.000\\ %",
                    CultureInfo.InvariantCulture);
                return cell.Ambiguous ? "~" + distance : distance;
            }
        }
        return "";
    }
}
