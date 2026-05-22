using System.Globalization;
using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class BinOverlapTableRenderer
{
    public static void Render(IReadOnlyList<BinOverlap> overlaps, IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Lower").RightAligned())
            .AddColumn(new TableColumn("Upper").RightAligned())
            .AddColumn(new TableColumn("c").RightAligned());

        foreach (var o in overlaps)
        {
            table.AddRow(
                o.Lower.ToString(),
                o.Upper.ToString(),
                o.Radius.ToString("P3", CultureInfo.InvariantCulture));
        }

        if (overlaps.Count == 0)
        {
            table.Caption("no overlaps");
        }
        else
        {
            var min = overlaps[0];
            for (var i = 1; i < overlaps.Count; i++)
            {
                if (overlaps[i].Radius < min.Radius) min = overlaps[i];
            }
            var minStr = min.Radius.ToString("P3", CultureInfo.InvariantCulture);
            table.Caption(
                $"{overlaps.Count} adjacent-pair overlap{(overlaps.Count == 1 ? "" : "s")}; " +
                $"minimum c = {minStr} at ({min.Lower} → {min.Upper})");
        }

        console.Write(table);
    }
}
