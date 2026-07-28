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
        console.Write(BuildTable(requestedKeys, ktet, rows, title: null));
    }

    // Render two key sets' supersets side-by-side as three tables: placements common to both,
    // then those unique to each. Common keys highlight against the union A∪B. Used by --compare.
    public static void RenderComparison(
        IReadOnlyCollection<int> keysA,
        IReadOnlyCollection<int> keysB,
        int ktet,
        IReadOnlyList<KeySupersetRow> common,
        IReadOnlyList<KeySupersetRow> onlyA,
        IReadOnlyList<KeySupersetRow> onlyB,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var union = keysA.Concat(keysB).Distinct().OrderBy(k => k).ToList();
        var aStr = KeySetString(keysA);
        var bStr = KeySetString(keysB);

        console.Write(BuildTable(union, ktet, common, title: $"Common: {aStr} ∩ {bStr}"));
        console.Write(BuildTable(keysA, ktet, onlyA, title: $"Only in --keys {aStr}"));
        console.Write(BuildTable(keysB, ktet, onlyB, title: $"Only in --compare {bStr}"));
    }

    private static Table BuildTable(
        IReadOnlyCollection<int> requestedKeys,
        int ktet,
        IReadOnlyList<KeySupersetRow> rows,
        string? title)
    {
        var table = new Table()
            .AddColumn(new TableColumn("LCM").RightAligned())
            .AddColumn(new TableColumn("Key").RightAligned())
            .AddColumn(new TableColumn($"Keys ({ktet}-tet)").LeftAligned())
            .AddColumn(new TableColumn("Extra").LeftAligned());

        if (title is not null) table.Title(title);

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

        table.Caption($"key-supersets: {KeySetString(requestedKeys)} · {rows.Count} placement{(rows.Count == 1 ? "" : "s")} · {ktet}-tet");
        return table;
    }

    private static string KeySetString(IReadOnlyCollection<int> keys) =>
        "{" + string.Join(", ", keys.OrderBy(k => k)) + "}";
}
