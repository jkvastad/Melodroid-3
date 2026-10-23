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

            // Superset rows first: one per containing placement (Origin = Bridge = Target, nothing to
            // factor), ordered by bridge then origin then target.
            var supersets = triplets
                .Where(t => t.Principle == ProgressionPrinciple.Superset)
                .OrderBy(t => Key(t.Bridge))
                .ThenBy(t => Key(t.Origin))
                .ThenBy(t => Key(t.Target));
            foreach (var t in supersets)
            {
                table.AddRow("[blue]superset[/]", Label(t.Origin), Label(t.Bridge), Label(t.Target));
            }

            // Subset rows: factor both Origin and Target around a shared Bridge (see GroupSubsetPairs).
            var subsets = triplets.Where(t => t.Principle == ProgressionPrinciple.Subset);
            foreach (var (origins, bridge, targets) in GroupSubsetPairs(subsets))
            {
                table.AddRow("[yellow]subset[/]", origins, bridge, targets);
            }

            console.Write(table);

            RenderSummary(triplets, console);
        }

        console.MarkupLine(
            $"[grey]progression: {originStr} → {targetStr} · {category.ToString().ToLowerInvariant()} · " +
            $"minSubsetNotes={minSubsetNotes} · bridges: {BridgeMode(collapseIsomorphic, poolMaximal)} · " +
            $"drop-collapsed: {dropCollapsed.ToString().ToLowerInvariant()} · 12-tet[/]");
    }

    // Per-principle placement summary beneath the triplet table: a quick read of which placements
    // participate overall. Superset collapses to one set (Origin = Bridge = Target); subset splits
    // by slot into origins (P_A) / bridges (S) / targets (P_B). Each set is deduped and
    // anchor-compressed. A principle's line is omitted when it has no triplets.
    private static void RenderSummary(IReadOnlyList<ProgressionTriplet> triplets, IAnsiConsole console)
    {
        var supersets = triplets.Where(t => t.Principle == ProgressionPrinciple.Superset).ToList();
        var subsets = triplets.Where(t => t.Principle == ProgressionPrinciple.Subset).ToList();

        console.MarkupLine("[grey]summary[/]");
        if (supersets.Count > 0)
            console.MarkupLine($"  [blue]superset[/]  {SummarizeSlot(supersets, t => t.Bridge)}");
        if (subsets.Count > 0)
            console.MarkupLine(
                $"  [yellow]subset[/]   origins {SummarizeSlot(subsets, t => t.Origin)} · " +
                $"bridges {SummarizeSlot(subsets, t => t.Bridge)} · " +
                $"targets {SummarizeSlot(subsets, t => t.Target)}");
    }

    // Distinct placements in one triplet slot, anchor-compressed (CompressAnchors handles ordering).
    private static string SummarizeSlot(
        IEnumerable<ProgressionTriplet> triplets, Func<ProgressionTriplet, Placement> slot) =>
        CompressAnchors(triplets.Select(t => Label(slot(t))).Distinct().ToList());

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
                if (target.SupersetBridges.Count > 0) via.Add($"sup {CapBraced(target.SupersetBridges, allBridges)}");
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
            .Select(prefix => $"{prefix}·{CompressAnchors(byPrefix[prefix])}")
            .ToList();
    }

    // Factor subset triplets for the standard (--target-keys) table two ways around a shared bridge S:
    // group origins that share the same bridge *and* the identical set of targets, then brace-list
    // both the origins and the targets. Each emitted row is a full Origins × {Bridge} × Targets
    // rectangle. This is lossless: SubsetTriplets excludes the diagonal P_A = P_B, so no origin can
    // appear in its own target set; since grouped origins share one target set, none of them is in it,
    // and every reconstructed cell is a real triple (never a same-placement progression).
    private static IEnumerable<(string Origins, string Bridge, string Targets)> GroupSubsetPairs(
        IEnumerable<ProgressionTriplet> subsetTriplets)
    {
        // (originLabel, bridgeLabel) → distinct target labels.
        var targetsByOriginBridge = new Dictionary<(string Origin, string Bridge), HashSet<string>>();
        foreach (var t in subsetTriplets)
        {
            var key = (Label(t.Origin), Label(t.Bridge));
            if (!targetsByOriginBridge.TryGetValue(key, out var targets))
            {
                targets = new HashSet<string>();
                targetsByOriginBridge[key] = targets;
            }
            targets.Add(Label(t.Target));
        }

        // Collapse (origin, bridge) entries sharing the same bridge and identical target set.
        var byGroup = new Dictionary<(string Bridge, string TargetSig), (string Bridge, List<string> Origins, List<string> Targets)>();
        foreach (var ((origin, bridge), targetSet) in targetsByOriginBridge)
        {
            var targets = targetSet.OrderBy(SortKey).ToList();
            var gkey = (bridge, string.Join(",", targets));
            if (!byGroup.TryGetValue(gkey, out var group))
            {
                group = (bridge, new List<string>(), targets);
                byGroup[gkey] = group;
            }
            group.Origins.Add(origin);
        }

        return byGroup.Values
            .OrderBy(g => SortKey(g.Bridge))
            .ThenBy(g => g.Origins.Select(SortKey).Min())
            .ThenBy(g => g.Targets.Select(SortKey).Min())
            .Select(g => (CompressAnchors(g.Origins), g.Bridge, CompressAnchors(g.Targets)));
    }

    // Render a brace list of "{Lcm}@{At}" labels, merging same-LCM entries to "{Lcm}@a,b,…" (anchors
    // ascending). A single label renders bare; two or more are wrapped in braces.
    private static string CompressAnchors(IReadOnlyList<string> labels)
    {
        var byLcm = new List<(int Lcm, List<int> Ats)>();
        foreach (var label in labels.OrderBy(SortKey))
        {
            var (lcm, at) = SortKey(label);
            if (byLcm.Count > 0 && byLcm[^1].Lcm == lcm)
                byLcm[^1].Ats.Add(at);
            else
                byLcm.Add((lcm, new List<int> { at }));
        }

        var joined = string.Join(", ", byLcm.Select(g => $"{g.Lcm}@{string.Join(",", g.Ats)}"));
        return labels.Count > 1 ? "{" + joined + "}" : joined;
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

    // Cap a superset bridge list, then render the kept labels as one anchor-compressed brace list
    // (CompressAnchors leaves a lone label bare). Overflow …(+M) trails outside the brace.
    private static string CapBraced(IReadOnlyList<string> bridges, bool showAll)
    {
        var overflow = !showAll && bridges.Count > MaxBridges;
        var kept = overflow ? bridges.Take(MaxBridges).ToList() : bridges;
        var body = CompressAnchors(kept);
        return overflow ? $"{body} …(+{bridges.Count - MaxBridges})" : body;
    }

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
