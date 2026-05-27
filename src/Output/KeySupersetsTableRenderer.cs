using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class KeySupersetsTableRenderer
{
    public static void Render(
        IReadOnlyCollection<int> requestedKeys,
        int ktet,
        IReadOnlyList<KeySupersetRow> rows,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("LCM").RightAligned())
            .AddColumn(new TableColumn("Key").RightAligned())
            .AddColumn(new TableColumn($"Keys ({ktet}-tet)").LeftAligned())
            .AddColumn(new TableColumn("Extra").LeftAligned());

        var requestedSet = requestedKeys.ToHashSet();
        foreach (var row in rows)
        {
            var keysStr = string.Join(" ", row.Placement.Keys.Select(k =>
                requestedSet.Contains(k) ? $"[green]{k}[/]" : k.ToString()));
            var extraStr = string.Join(" ", row.Placement.Keys.Where(k => !requestedSet.Contains(k)));
            table.AddRow(
                row.Placement.Lcm.ToString(),
                row.Placement.At.ToString(),
                keysStr,
                extraStr);
        }

        var requestedStr = "{" + string.Join(", ", requestedKeys.OrderBy(k => k)) + "}";
        table.Caption($"key-supersets: {requestedStr} · {rows.Count} placement{(rows.Count == 1 ? "" : "s")} · {ktet}-tet");
        console.Write(table);
    }
}
