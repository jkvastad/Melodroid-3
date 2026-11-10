using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class CompositeScaleTableRenderer
{
    // One table: each composite scale as a concrete 12-tet key set, its size, and the P_A·S·P_B
    // constituent placements it is composed of. Scales arrive pre-sorted (size, then keys, then
    // constituents) from CompositeScales.Enumerate.
    public static void Render(
        IReadOnlyList<CompositeScale> scales,
        int minSubsetNotes,
        bool stable,
        bool collapseIsomorphic,
        bool poolMaximal,
        bool dedupeTransposition,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        if (scales.Count == 0)
        {
            console.MarkupLine("[grey]composite-scales: none found[/]");
        }
        else
        {
            var table = new Table()
                .AddColumn("Scale")
                .AddColumn(new TableColumn("Size").RightAligned())
                .AddColumn("P_A·S·P_B");

            foreach (var s in scales)
            {
                table.AddRow(
                    KeySet(s.Keys),
                    s.Keys.Count.ToString(),
                    $"{Label(s.PA)}·{Label(s.S)}·{Label(s.PB)}");
            }

            console.Write(table);
        }

        console.MarkupLine(
            $"[grey]composite-scales: {scales.Count} scale{(scales.Count == 1 ? "" : "s")} · " +
            $"minSubsetNotes={minSubsetNotes} · stable={BoolText(stable)} · " +
            $"bridges: {BridgeMode(collapseIsomorphic, poolMaximal)} · " +
            $"dedupe-transposition={BoolText(dedupeTransposition)} · 12-tet[/]");
    }

    private static string Label(Placement p) => $"{p.Lcm}@{p.At}";

    private static string KeySet(IReadOnlyCollection<int> keys) =>
        "{" + string.Join(", ", keys.OrderBy(k => k)) + "}";

    private static string BoolText(bool value) => value ? "on" : "off";

    // "raw" when neither declutter is active, else the active ones (e.g. "collapse-isomorphic+pool-maximal").
    private static string BridgeMode(bool collapseIsomorphic, bool poolMaximal)
    {
        var parts = new List<string>();
        if (collapseIsomorphic) parts.Add("collapse-isomorphic");
        if (poolMaximal) parts.Add("pool-maximal");
        return parts.Count == 0 ? "raw" : string.Join("+", parts);
    }
}
