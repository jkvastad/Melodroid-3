using System.Globalization;
using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class KeysNeededTableRenderer
{
    public static void Render(
        IReadOnlyList<KeysNeededRow> rows,
        int maxSize,
        int maxPrime,
        double startBinRadius,
        double maxBinRadius,
        double radiusStep,
        int maxK,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Bin Radius").RightAligned())
            .AddColumn(new TableColumn("Bin Radius %").RightAligned())
            .AddColumn(new TableColumn("Min k").RightAligned());

        int? overallMinK = null;
        var bestSoFar = int.MaxValue;
        foreach (var row in rows)
        {
            string? colour = null;
            if (row.MinK is null)
            {
                colour = "red";
            }
            else if (row.MinK.Value < bestSoFar)
            {
                bestSoFar = row.MinK.Value;
                overallMinK = bestSoFar;
                colour = "green";
            }

            var radiusCell = row.BinRadius.ToString("F6", CultureInfo.InvariantCulture);
            var radiusPctCell = row.BinRadius.ToString("P4", CultureInfo.InvariantCulture);
            var minKCell = row.MinK?.ToString(CultureInfo.InvariantCulture) ?? "—";

            var cells = new[] { radiusCell, radiusPctCell, minKCell };
            if (colour is not null)
            {
                for (var i = 0; i < cells.Length; i++)
                {
                    cells[i] = $"[{colour}]{Markup.Escape(cells[i])}[/]";
                }
            }
            else
            {
                for (var i = 0; i < cells.Length; i++)
                {
                    cells[i] = Markup.Escape(cells[i]);
                }
            }
            table.AddRow(cells);
        }

        var startPct = startBinRadius.ToString("P4", CultureInfo.InvariantCulture);
        var maxPct = maxBinRadius.ToString("P4", CultureInfo.InvariantCulture);
        var stepPct = radiusStep.ToString("P4", CultureInfo.InvariantCulture);
        var minKClause = overallMinK is { } mk
            ? $"minimum k reached = {mk}"
            : "no k ≤ " + maxK + " covered all good fractions at any swept radius";
        table.Caption(
            $"{rows.Count} radius step{(rows.Count == 1 ? "" : "s")}; " +
            $"sweep {startBinRadius.ToString("F6", CultureInfo.InvariantCulture)} ({startPct}) " +
            $"→ {maxBinRadius.ToString("F6", CultureInfo.InvariantCulture)} ({maxPct}) " +
            $"step {radiusStep.ToString("F6", CultureInfo.InvariantCulture)} ({stepPct}); " +
            $"good fractions from --max-size {maxSize} --max-prime {maxPrime}; " +
            $"--max-k {maxK}; {minKClause}");

        console.Write(table);
    }
}
