using System.Globalization;
using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class KtetCutoffsTableRenderer
{
    public static void Render(
        IReadOnlyList<KtetCutoffRow> rows,
        int maxSize,
        int maxPrime,
        int maxK,
        bool onlyStrictlyImproving = false,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("k").RightAligned())
            .AddColumn(new TableColumn("c_k").RightAligned())
            .AddColumn(new TableColumn("c_k %").RightAligned())
            .AddColumn(new TableColumn("Limiting Fraction").LeftAligned())
            .AddColumn(new TableColumn("Nearest n").RightAligned())
            .AddColumn(new TableColumn("2^(n/k)").RightAligned());

        var activeKs = new List<int>();
        var bestSoFar = double.PositiveInfinity;
        foreach (var row in rows)
        {
            var isActive = row.Radius < bestSoFar;
            if (isActive)
            {
                bestSoFar = row.Radius;
                activeKs.Add(row.K);
            }

            if (onlyStrictlyImproving && !isActive) continue;

            var cells = new[]
            {
                row.K.ToString(CultureInfo.InvariantCulture),
                row.Radius.ToString("F6", CultureInfo.InvariantCulture),
                row.Radius.ToString("P4", CultureInfo.InvariantCulture),
                row.LimitingFraction.ToString(),
                row.KeyIndex.ToString(CultureInfo.InvariantCulture),
                row.KeyRatio.ToString("F6", CultureInfo.InvariantCulture),
            };

            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = isActive
                    ? $"[green]{Markup.Escape(cells[i])}[/]"
                    : Markup.Escape(cells[i]);
            }
            table.AddRow(cells);
        }

        var activeClause = activeKs.Count == 0
            ? "no active k (empty fraction set)"
            : "active k sequence: " + string.Join(" → ", activeKs);
        var filterClause = onlyStrictlyImproving ? " (filter: only strictly improving)" : "";
        table.Caption(
            $"{rows.Count} k value{(rows.Count == 1 ? "" : "s")}; " +
            $"good fractions from --max-size {maxSize} --max-prime {maxPrime}; " +
            $"--max-k {maxK}; {activeClause}{filterClause}");

        console.Write(table);
    }
}
