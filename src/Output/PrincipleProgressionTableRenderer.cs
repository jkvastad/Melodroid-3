using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class PrincipleProgressionTableRenderer
{
    // Cap the bridge lists so a "Via" cell stays readable; overflow is summarised as …(+M).
    private const int MaxBridges = 6;

    public static void Render(
        IReadOnlyCollection<int> chordKeys,
        int minSubsetNotes,
        bool rawBridges,
        IReadOnlyList<PrincipleTarget> targets,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        // One table per triad quality: the quality is the title, so root alone identifies the chord.
        var qualityOrder = new[] { TriadQuality.Major, TriadQuality.Minor, TriadQuality.Diminished };
        foreach (var quality in qualityOrder)
        {
            var group = targets.Where(t => t.Quality == quality).OrderBy(t => t.Root).ToList();
            if (group.Count == 0) continue;

            var table = new Table()
                .Title(QualityName(quality))
                .AddColumn(new TableColumn("Root").RightAligned())
                .AddColumn(new TableColumn("Category"))
                .AddColumn(new TableColumn("Via"));

            foreach (var target in group)
            {
                var via = new List<string>();
                if (target.SupersetBridges.Count > 0) via.Add($"sup {Cap(target.SupersetBridges)}");
                if (target.SubsetBridges.Count > 0) via.Add($"sub {Cap(target.SubsetBridges)}");

                table.AddRow(
                    target.Root.ToString(),
                    CategoryMarkup(target.Category),
                    string.Join(" · ", via));
            }

            console.Write(table);
        }

        var chordStr = "{" + string.Join(", ", chordKeys.OrderBy(k => k)) + "}";
        console.MarkupLine(
            $"[grey]principle-progression: chord={chordStr} · minSubsetNotes={minSubsetNotes} · " +
            $"bridges: {(rawBridges ? "raw" : "pooled")} · " +
            $"{targets.Count} target{(targets.Count == 1 ? "" : "s")} · 12-tet[/]");
    }

    private static string Cap(IReadOnlyList<string> bridges) =>
        bridges.Count <= MaxBridges
            ? string.Join(" ", bridges)
            : string.Join(" ", bridges.Take(MaxBridges)) + $" …(+{bridges.Count - MaxBridges})";

    private static string CategoryMarkup(ProgressionCategory category) => category switch
    {
        ProgressionCategory.Both => "[green]both[/]",
        ProgressionCategory.Superset => "[blue]superset[/]",
        ProgressionCategory.Subset => "[yellow]subset[/]",
        _ => "[grey]neither[/]",
    };

    private static string QualityName(TriadQuality quality) => quality switch
    {
        TriadQuality.Major => "major",
        TriadQuality.Minor => "minor",
        _ => "dim",
    };
}
