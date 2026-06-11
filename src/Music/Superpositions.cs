namespace Melodroid_3.Music;

public readonly record struct Superposition(
    IReadOnlyList<Placement> Pieces,   // sorted by (Lcm, At)
    IReadOnlyList<int> ExtraKeys,      // union(pieces' keys) \ target, sorted
    bool DisjointOnTarget,             // pieces' target-contributions partition the target
    int? Reference,                    // shared anchor when unique-reference; null otherwise
    int? CombinedLcm);                 // lcm of pieces' family LCMs when unique-reference (shared fundamental); null otherwise

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
    /// <para>
    /// When <paramref name="uniqueReference"/> is true, only covers whose pieces all share one
    /// reference key (anchor) are returned, and each result is tagged with that key. The reference
    /// of a placement <c>L@a</c> is its anchor <c>a</c> — the key where the family's fundamental
    /// (1/1, present in every family) sits. Aliases between isomorphic families are handled
    /// implicitly: at a fixed anchor r, isomorphic families surface as identical keyboard key-sets,
    /// so restricting candidates to <c>At == r</c> is exactly "pieces that can share reference r".
    /// </para>
    /// </summary>
    public static (IReadOnlyList<Superposition> Results, bool Truncated) Enumerate(
        IReadOnlyCollection<int> targetKeys,
        IReadOnlyList<LcmFamily> families,
        int ktet,
        int minBlockLcm,
        int maxBlockLcm,
        int maxResults,
        bool uniqueReference)
    {
        var target = new HashSet<int>(targetKeys);
        if (target.Count == 0 || maxResults < 1)
            return (Array.Empty<Superposition>(), false);

        // 1. Candidate pieces: placements that touch the target, deduped by full key-set
        //    (smallest (Lcm, At) representative). For unique-reference we build one list per
        //    anchor; otherwise a single list across all anchors.
        var lists = new List<(IReadOnlyList<Candidate> Candidates, int? Reference)>();
        if (uniqueReference)
        {
            for (var r = 0; r < ktet; r++)
                lists.Add((BuildCandidates(target, families, ktet, minBlockLcm, maxBlockLcm, anchor: r), r));
        }
        else
        {
            lists.Add((BuildCandidates(target, families, ktet, minBlockLcm, maxBlockLcm, anchor: null), null));
        }

        // 2. Enumerate minimal covers, fewest pieces first, by iterative deepening on cover
        //    size (a minimal/irredundant cover never needs more pieces than there are target
        //    keys). Deepening across every candidate list in lockstep keeps the truncation
        //    guarantee global: hitting the cap only ever drops covers tied for the largest size
        //    reached — never a smaller (better-ranked) one in some other list.
        var covers = new List<(List<Candidate> Cover, int? Reference)>();
        var seenPerList = lists.Select(_ => new HashSet<string>()).ToArray();
        var truncated = false;

        for (var depthLimit = 1; depthLimit <= target.Count && covers.Count < maxResults; depthLimit++)
        {
            for (var i = 0; i < lists.Count; i++)
            {
                if (covers.Count >= maxResults) { truncated = true; break; }
                truncated |= SearchPass(
                    lists[i].Candidates, target, depthLimit, maxResults,
                    seenPerList[i], covers, lists[i].Reference);
            }
        }

        // 3. Build Superposition rows.
        var results = new List<Superposition>(covers.Count);
        foreach (var (cover, reference) in covers)
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

            // Unique-reference: all pieces share one fundamental, so the whole superposition's
            // wave pattern length is the lcm of the constituent family LCMs. With pieces on
            // different references (any-reference) there is no single fundamental, so leave it null.
            int? combinedLcm = reference is null
                ? null
                : pieces.Select(p => p.Lcm).Aggregate(IntegerMath.Lcm);

            results.Add(new Superposition(pieces, extraKeys, disjoint, reference, combinedLcm));
        }

        // 4. Sort: piece count ↑, total extras ↑, max block LCM ↑, reference ↑, then (Lcm, At).
        results = results
            .OrderBy(s => s.Pieces.Count)
            .ThenBy(s => s.ExtraKeys.Count)
            .ThenBy(s => s.Pieces.Max(p => p.Lcm))
            .ThenBy(s => s.Reference ?? -1)
            .ThenBy(s => string.Join(";", s.Pieces.Select(p => $"{p.Lcm}@{p.At}")))
            .ToList();

        return (results, truncated);
    }

    /// <summary>
    /// Build the deduped candidate pieces for one search. When <paramref name="anchor"/> is set,
    /// only placements at that anchor are considered (and dedup keeps the smallest-LCM family that
    /// reaches a given key-set there — this is what surfaces an alias such as 12@7 when LCM 8 can't
    /// reach that key-set at anchor 7).
    /// </summary>
    private static List<Candidate> BuildCandidates(
        HashSet<int> target,
        IReadOnlyList<LcmFamily> families,
        int ktet,
        int minBlockLcm,
        int maxBlockLcm,
        int? anchor)
    {
        var byKeySet = new Dictionary<string, Candidate>();
        foreach (var family in families)
        {
            if (family.Lcm < minBlockLcm || family.Lcm > maxBlockLcm) continue;

            var placements = anchor.HasValue
                ? new[] { Placements.Compute(family, anchor.Value, ktet) }
                : (IEnumerable<Placement>)Placements.Sweep(family, ktet);

            foreach (var placement in placements)
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

        return byKeySet.Values
            .OrderBy(c => c.Placement.Lcm)
            .ThenBy(c => c.Placement.At)
            .Select((c, i) => c with { Id = i })
            .ToList();
    }

    /// <summary>
    /// One iterative-deepening pass over a single candidate list at the given cover-size limit.
    /// Branches on the smallest uncovered target key, forbidding earlier siblings so each cover is
    /// generated once. Found covers are appended (tagged with <paramref name="reference"/>) until
    /// the global <paramref name="maxResults"/> cap. Returns true if the cap was hit.
    /// </summary>
    private static bool SearchPass(
        IReadOnlyList<Candidate> candidates,
        HashSet<int> target,
        int depthLimit,
        int maxResults,
        HashSet<string> seen,
        List<(List<Candidate> Cover, int? Reference)> covers,
        int? reference)
    {
        var truncated = false;

        void Search(HashSet<int> uncovered, List<Candidate> chosen, HashSet<int> forbidden)
        {
            if (covers.Count >= maxResults) { truncated = true; return; }
            if (uncovered.Count == 0)
            {
                if (!IsMinimal(chosen, target)) return;
                var key = string.Join(",", chosen.Select(c => c.Id).OrderBy(x => x));
                if (seen.Add(key)) covers.Add((new List<Candidate>(chosen), reference));
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
                Search(nextUncovered, chosen, childForbidden);
                chosen.RemoveAt(chosen.Count - 1);
            }
        }

        Search(new HashSet<int>(target), new List<Candidate>(), new HashSet<int>());
        return truncated;
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
