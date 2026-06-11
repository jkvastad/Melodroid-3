namespace Melodroid_3.Music;

public readonly record struct Superposition(
    IReadOnlyList<Placement> Pieces,   // sorted by (Lcm, At)
    IReadOnlyList<int> ExtraKeys,      // union(pieces' keys) \ target, sorted
    bool DisjointOnTarget);            // pieces' target-contributions partition the target

public static class Superpositions
{
    private sealed record Candidate(
        int Id,
        Placement Placement,
        IReadOnlyList<int> FullKeys,        // distinct, sorted
        HashSet<int> Contribution);          // FullKeys ∩ target

    /// <summary>
    /// Enumerate minimal/irredundant ways to cover <paramref name="targetKeys"/> with a union
    /// of LCM-family placements (each family with <paramref name="minBlockLcm"/> ≤ Lcm ≤
    /// <paramref name="maxBlockLcm"/>, swept over all k anchors). Building blocks may extend past
    /// the target; their union must contain every target key. A cover is irredundant iff every
    /// piece uniquely covers ≥1 target key. Returns at most <paramref name="maxResults"/> covers;
    /// <c>Truncated</c> is true when enumeration stopped at that cap.
    /// </summary>
    public static (IReadOnlyList<Superposition> Results, bool Truncated) Enumerate(
        IReadOnlyCollection<int> targetKeys,
        IReadOnlyList<LcmFamily> families,
        int ktet,
        int minBlockLcm,
        int maxBlockLcm,
        int maxResults)
    {
        var target = new HashSet<int>(targetKeys);
        if (target.Count == 0 || maxResults < 1)
            return (Array.Empty<Superposition>(), false);

        // 1. Candidate pieces: placements that touch the target, deduped by full key-set.
        var byKeySet = new Dictionary<string, Candidate>();
        foreach (var family in families)
        {
            if (family.Lcm < minBlockLcm || family.Lcm > maxBlockLcm) continue;
            foreach (var placement in Placements.Sweep(family, ktet))
            {
                var fullKeys = placement.Keys.Distinct().OrderBy(x => x).ToList();
                var contribution = new HashSet<int>(fullKeys.Where(target.Contains));
                if (contribution.Count == 0) continue;

                var key = string.Join(",", fullKeys);
                // Keep the representative with the smallest (Lcm, At) for identical key-sets.
                if (byKeySet.TryGetValue(key, out var existing) &&
                    (existing.Placement.Lcm < placement.Lcm ||
                     (existing.Placement.Lcm == placement.Lcm && existing.Placement.At <= placement.At)))
                    continue;
                byKeySet[key] = new Candidate(-1, placement, fullKeys, contribution);
            }
        }

        var candidates = byKeySet.Values
            .OrderBy(c => c.Placement.Lcm)
            .ThenBy(c => c.Placement.At)
            .Select((c, i) => c with { Id = i })
            .ToList();

        // 2. Enumerate minimal covers, fewest pieces first, by iterative deepening on cover
        //    size (a minimal/irredundant cover never needs more pieces than there are target
        //    keys). Within a pass we branch on the smallest uncovered target key, forbidding
        //    earlier siblings so each cover is generated once. Deepening before the cap means
        //    truncation only ever drops covers tied for the largest size reached — never a
        //    smaller (better-ranked) one.
        var covers = new List<List<Candidate>>();
        var seen = new HashSet<string>();
        var truncated = false;

        void Search(HashSet<int> uncovered, List<Candidate> chosen, HashSet<int> forbidden, int depthLimit)
        {
            if (covers.Count >= maxResults) { truncated = true; return; }
            if (uncovered.Count == 0)
            {
                if (!IsMinimal(chosen, target)) return;
                var key = string.Join(",", chosen.Select(c => c.Id).OrderBy(x => x));
                if (seen.Add(key)) covers.Add(new List<Candidate>(chosen));
                return;
            }
            if (chosen.Count >= depthLimit) return; // can't finish within the size limit

            var u = uncovered.Min();
            var coveringU = candidates
                .Where(c => !forbidden.Contains(c.Id) && c.Contribution.Contains(u))
                .ToList();

            for (var i = 0; i < coveringU.Count; i++)
            {
                if (covers.Count >= maxResults) { truncated = true; return; }
                var c = coveringU[i];
                var childForbidden = new HashSet<int>(forbidden);
                for (var j = 0; j < i; j++) childForbidden.Add(coveringU[j].Id);

                var nextUncovered = new HashSet<int>(uncovered);
                nextUncovered.ExceptWith(c.Contribution);
                chosen.Add(c);
                Search(nextUncovered, chosen, childForbidden, depthLimit);
                chosen.RemoveAt(chosen.Count - 1);
            }
        }

        for (var depthLimit = 1; depthLimit <= target.Count && covers.Count < maxResults; depthLimit++)
            Search(new HashSet<int>(target), new List<Candidate>(), new HashSet<int>(), depthLimit);

        // 3. Build Superposition rows.
        var results = new List<Superposition>(covers.Count);
        foreach (var cover in covers)
        {
            var pieces = cover
                .Select(c => c.Placement)
                .OrderBy(p => p.Lcm)
                .ThenBy(p => p.At)
                .ToList();

            var union = new HashSet<int>();
            foreach (var c in cover) union.UnionWith(c.FullKeys);
            var extraKeys = union.Where(x => !target.Contains(x)).OrderBy(x => x).ToList();

            var disjoint = cover.Sum(c => c.Contribution.Count) == target.Count;

            results.Add(new Superposition(pieces, extraKeys, disjoint));
        }

        // 4. Sort: piece count ↑, total extras ↑, max block LCM ↑, then (Lcm, At) sequence.
        results = results
            .OrderBy(s => s.Pieces.Count)
            .ThenBy(s => s.ExtraKeys.Count)
            .ThenBy(s => s.Pieces.Max(p => p.Lcm))
            .ThenBy(s => string.Join(";", s.Pieces.Select(p => $"{p.Lcm}@{p.At}")))
            .ToList();

        return (results, truncated);
    }

    private static bool IsMinimal(IReadOnlyList<Candidate> cover, HashSet<int> target)
    {
        for (var i = 0; i < cover.Count; i++)
        {
            var othersUnion = new HashSet<int>();
            for (var j = 0; j < cover.Count; j++)
                if (j != i) othersUnion.UnionWith(cover[j].Contribution);
            if (target.IsSubsetOf(othersUnion)) return false; // cover[i] is redundant
        }
        return true;
    }
}
