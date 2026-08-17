using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class ProgressionTableRenderer
{
    public static void Render(
        IReadOnlyCollection<int> chordKeys,
        bool includeSupersets,
        bool includeAdjacency,
        IReadOnlyList<ProgressionTarget> targets,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Next Chord").RightAligned())
            .AddColumn(new TableColumn("Quality"))
            .AddColumn(new TableColumn("Root").RightAligned())
            .AddColumn(new TableColumn("Rule"))
            .AddColumn(new TableColumn("Via"));

        foreach (var target in targets)
        {
            var hasSuperset = target.SupersetBridges.Count > 0;
            var hasAdjacency = target.AdjacencyBridges.Count > 0;
            var rule = hasSuperset && hasAdjacency ? "both" : hasSuperset ? "superset" : "adjacency";

            var via = new List<string>();
            if (hasSuperset) via.Add(string.Join(" ", target.SupersetBridges));
            if (hasAdjacency) via.Add($"+adj {string.Join(" ", target.AdjacencyBridges)}");

            table.AddRow(
                string.Join(" ", target.Keys),
                QualityName(target.Quality),
                target.Root.ToString(),
                rule,
                string.Join(" · ", via));
        }

        var ruleLabel = includeSupersets && includeAdjacency ? "both" : includeSupersets ? "supersets" : "adjacency";
        var chordStr = "{" + string.Join(", ", chordKeys.OrderBy(k => k)) + "}";
        table.Caption(
            $"progression: chord={chordStr} · rule: {ruleLabel} · " +
            $"{targets.Count} target{(targets.Count == 1 ? "" : "s")} · 12-tet");
        console.Write(table);
    }

    private static string QualityName(TriadQuality quality) => quality switch
    {
        TriadQuality.Major => "major",
        TriadQuality.Minor => "minor",
        _ => "dim",
    };
}
