using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class ProgressionTableRenderer
{
    // Cap the bridge lists so a "Via" cell stays readable; overflow is summarised as …(+M).
    private const int MaxBridges = 6;

    public static void Render(
        IReadOnlyCollection<int> chordKeys,
        int minSubsetNotes,
        bool rawBridges,
        CollapseFilter dropCollapsed,
        IReadOnlyList<ProgressionTarget> targets,
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
                if (target.SubsetBridges.Count > 0) via.Add($"sub {Cap(GroupSubsetTriples(target.SubsetBridges))}");

                table.AddRow(
                    target.Root.ToString(),
                    CategoryMarkup(target.Category),
                    string.Join(" · ", via));
            }

            console.Write(table);
        }

        var chordStr = "{" + string.Join(", ", chordKeys.OrderBy(k => k)) + "}";
        console.MarkupLine(
            $"[grey]progression: chord={chordStr} · minSubsetNotes={minSubsetNotes} · " +
            $"bridges: {(rawBridges ? "raw" : "pooled")} · " +
            $"drop-collapsed: {dropCollapsed.ToString().ToLowerInvariant()} · " +
            $"{targets.Count} target{(targets.Count == 1 ? "" : "s")} · 12-tet[/]");
    }

    // Factor a flat list of "P_A·S·P_B" subset triples by their shared "P_A·S" prefix, brace-listing
    // the varying P_B. Purely visual — triples sharing a source placement and shared subset collapse
    // to "P_A·S·{P_B, …}" (or "P_A·S·P_B" when a single target). Applies to raw and pooled lists alike.
    private static List<string> GroupSubsetTriples(IReadOnlyList<string> triples)
    {
        var order = new List<string>();
        var byPrefix = new Dictionary<string, List<string>>();
        foreach (var triple in triples)
        {
            var parts = triple.Split('·');
            var prefix = parts[0] + "·" + parts[1];
            if (!byPrefix.TryGetValue(prefix, out var pbs))
            {
                pbs = new List<string>();
                byPrefix[prefix] = pbs;
                order.Add(prefix);
            }
            if (!pbs.Contains(parts[2])) pbs.Add(parts[2]);
        }

        return order
            .OrderBy(prefix => SortKey(prefix.Split('·')[0]))
            .ThenBy(prefix => SortKey(prefix.Split('·')[1]))
            .Select(prefix =>
            {
                var pbs = byPrefix[prefix].OrderBy(SortKey).ToList();
                return pbs.Count == 1
                    ? $"{prefix}·{pbs[0]}"
                    : $"{prefix}·{{{string.Join(", ", pbs)}}}";
            })
            .ToList();
    }

    // Sort key for a single "{Lcm}@{At}" label: by LCM then anchor.
    private static (int Lcm, int At) SortKey(string label)
    {
        var parts = label.Split('@');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
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
