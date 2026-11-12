using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class ProgressionTableRenderer
{
    // One ordered chord pair rendered as its placement-progression triplets (unified
    // origin/bridge/target rows — single-placement progressions first, then bridged, each sorted by
    // bridge/origin/target), or — when no placement progression exists — its composite-scale hosts.
    public static void RenderPair(
        IReadOnlyCollection<int> origin,
        IReadOnlyCollection<int> target,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        CollapseFilter dropCollapsed,
        ProgressionReach reach,
        IReadOnlyList<ProgressionTriplet> triplets,
        IReadOnlyList<string> compositeBridges,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var originStr = KeySet(origin);
        var targetStr = KeySet(target);

        if (triplets.Count > 0)
        {
            var table = new Table()
                .Title($"{originStr} → {targetStr}")
                .AddColumn("Origin")
                .AddColumn("Bridge")
                .AddColumn("Target");

            // Single-placement progressions first: one per placement holding both chords
            // (Origin = Bridge = Target, nothing to factor), ordered by bridge then origin then target.
            var single = triplets
                .Where(t => t.Principle == ProgressionPrinciple.Superset)
                .OrderBy(t => Key(t.Bridge))
                .ThenBy(t => Key(t.Origin))
                .ThenBy(t => Key(t.Target));
            foreach (var t in single)
            {
                table.AddRow(Label(t.Origin), Label(t.Bridge), Label(t.Target));
            }

            // Bridged progressions: factor both Origin and Target around a shared Bridge (GroupSubsetPairs).
            var bridged = triplets.Where(t => t.Principle == ProgressionPrinciple.Subset);
            foreach (var (origins, bridge, targets) in GroupSubsetPairs(bridged))
            {
                table.AddRow(origins, bridge, targets);
            }

            console.Write(table);

            RenderSummary(triplets, console);
        }
        else if (compositeBridges.Count > 0)
        {
            console.MarkupLine(
                $"{originStr} → {targetStr}: [yellow]composite[/] — hosted by {string.Join(", ", compositeBridges)}");
        }
        else
        {
            console.MarkupLine($"{originStr} → {targetStr}: [grey]unreachable — no progression found[/]");
        }

        console.MarkupLine(
            $"[grey]progression: {originStr} → {targetStr} · {reach.ToString().ToLowerInvariant()} · " +
            $"minSubsetNotes={minSubsetNotes} · bridges: {BridgeMode(collapseIsomorphic, poolMaximal)} · " +
            $"drop-collapsed: {dropCollapsed.ToString().ToLowerInvariant()} · 12-tet[/]");
    }

    // Placement summary beneath the triplet table: a quick read of which placements participate overall.
    // Single-placement progressions collapse to one set (Origin = Bridge = Target); bridged progressions
    // split by slot into origins (P_A) / bridges (S) / targets (P_B). Each set is deduped and
    // anchor-compressed. A shape's line is omitted when it has no triplets.
    private static void RenderSummary(IReadOnlyList<ProgressionTriplet> triplets, IAnsiConsole console)
    {
        var single = triplets.Where(t => t.Principle == ProgressionPrinciple.Superset).ToList();
        var bridged = triplets.Where(t => t.Principle == ProgressionPrinciple.Subset).ToList();

        console.MarkupLine("[grey]summary[/]");
        if (single.Count > 0)
            console.MarkupLine($"  [green]holds both[/]  {SummarizeSlot(single, t => t.Bridge)}");
        if (bridged.Count > 0)
            console.MarkupLine(
                $"  [yellow]bridged[/]     origins {SummarizeSlot(bridged, t => t.Origin)} · " +
                $"bridges {SummarizeSlot(bridged, t => t.Bridge)} · " +
                $"targets {SummarizeSlot(bridged, t => t.Target)}");
    }

    // Distinct placements in one triplet slot, anchor-compressed (CompressAnchors handles ordering).
    private static string SummarizeSlot(
        IEnumerable<ProgressionTriplet> triplets, Func<ProgressionTriplet, Placement> slot) =>
        CompressAnchors(triplets.Select(t => Label(slot(t))).Distinct().ToList());

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
}
