using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class ProgressionTableRenderer
{
    // Cap the convenience-table bridge lists so a "Via" cell stays readable; overflow is …(+M).
    private const int MaxBridges = 6;

    // Standard form: one ordered chord pair rendered as explicit origin/bridge/target triplet rows,
    // superset rows first, then subset — each sorted by bridge, then origin, then target.
    public static void RenderPair(
        IReadOnlyCollection<int> origin,
        IReadOnlyCollection<int> target,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        CollapseFilter dropCollapsed,
        ProgressionCategory category,
        IReadOnlyList<ProgressionTriplet> triplets,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var originStr = KeySet(origin);
        var targetStr = KeySet(target);

        if (triplets.Count == 0)
        {
            console.MarkupLine($"{originStr} → {targetStr}: [grey]neither — no progression found[/]");
        }
        else
        {
            var table = new Table()
                .Title($"{originStr} → {targetStr}")
                .AddColumn("Principle")
                .AddColumn("Origin")
                .AddColumn("Bridge")
                .AddColumn("Target");

            var ordered = triplets
                .OrderBy(t => t.Principle == ProgressionPrinciple.Superset ? 0 : 1)
                .ThenBy(t => Key(t.Bridge))
                .ThenBy(t => Key(t.Origin))
                .ThenBy(t => Key(t.Target));

            foreach (var t in ordered)
            {
                table.AddRow(
                    t.Principle == ProgressionPrinciple.Superset ? "[blue]superset[/]" : "[yellow]subset[/]",
                    Label(t.Origin),
                    Label(t.Bridge),
                    Label(t.Target));
            }

            console.Write(table);
        }

        console.MarkupLine(
            $"[grey]progression: {originStr} → {targetStr} · {category.ToString().ToLowerInvariant()} · " +
            $"minSubsetNotes={minSubsetNotes} · bridges: {BridgeMode(collapseIsomorphic, poolMaximal)} · " +
            $"drop-collapsed: {dropCollapsed.ToString().ToLowerInvariant()} · 12-tet[/]");
    }

    // Convenience form: one table per triad quality, a row per candidate target root.
    public static void Render(
        IReadOnlyCollection<int> chordKeys,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        CollapseFilter dropCollapsed,
        IReadOnlyList<ProgressionTarget> targets,
        bool allBridges = false,
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
                if (target.SupersetBridges.Count > 0) via.Add($"sup {Cap(target.SupersetBridges, allBridges)}");
                if (target.SubsetBridges.Count > 0) via.Add($"sub {Cap(GroupSubsetTriples(target.SubsetBridges), allBridges)}");

                table.AddRow(
                    target.Root.ToString(),
                    CategoryMarkup(target.Category),
                    string.Join(" · ", via));
            }

            console.Write(table);
        }

        console.MarkupLine(
            $"[grey]progression: chord={KeySet(chordKeys)} · minSubsetNotes={minSubsetNotes} · " +
            $"bridges: {BridgeMode(collapseIsomorphic, poolMaximal)}{(allBridges ? ", all" : "")} · " +
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

    private static string Label(Placement p) => $"{p.Lcm}@{p.At}";

    private static (int Lcm, int At) Key(Placement p) => (p.Lcm, p.At);

    private static string KeySet(IReadOnlyCollection<int> keys) =>
        "{" + string.Join(", ", keys.OrderBy(k => k)) + "}";

    // "raw" when neither declutter is active, else the active ones (e.g. "collapse-isomorphic+pool-maximal").
    private static string BridgeMode(bool collapseIsomorphic, bool poolMaximal)
    {
        var parts = new List<string>();
        if (collapseIsomorphic) parts.Add("collapse-isomorphic");
        if (poolMaximal) parts.Add("pool-maximal");
        return parts.Count == 0 ? "raw" : string.Join("+", parts);
    }

    // Sort key for a single "{Lcm}@{At}" label: by LCM then anchor.
    private static (int Lcm, int At) SortKey(string label)
    {
        var parts = label.Split('@');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    private static string Cap(IReadOnlyList<string> bridges, bool showAll) =>
        showAll || bridges.Count <= MaxBridges
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
