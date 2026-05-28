namespace Melodroid_3.Music;

public readonly record struct Voicing(
    int Root,
    IReadOnlyList<int> Residues,
    IReadOnlyList<int> Intervals,
    int Span,
    int Penalty);

public static class Voicings
{
    public static int Penalty(int interval)
    {
        if (interval == 3 || interval == 4) return 0;
        if (interval == 2 || interval == 5) return 1;
        if (interval >= 6) return interval - 4;
        return int.MaxValue / 2;
    }

    public static IReadOnlyList<Voicing> EnumerateAll(IReadOnlyList<int> placementKeys, int ktet)
    {
        var distinctKeys = placementKeys.Distinct().OrderBy(k => k).ToList();
        var results = new List<Voicing>();

        foreach (var root in distinctKeys)
        {
            var residues = new List<int> { root };
            var intervals = new List<int>();
            var used = new HashSet<int> { root };
            Search(root, residues, intervals, used, distinctKeys, ktet, runningPenalty: 0, results);
        }

        return results;
    }

    public static IReadOnlyList<Voicing> EnumerateBestPerRoot(IReadOnlyList<int> placementKeys, int ktet)
    {
        var all = EnumerateAll(placementKeys, ktet);
        var comparer = new ResidueLexComparer();
        var result = new List<Voicing>();

        foreach (var group in all.GroupBy(v => v.Root).OrderBy(g => g.Key))
        {
            var minPenalty = group.Min(v => v.Penalty);
            var kept = group
                .Where(v => v.Penalty == minPenalty)
                .OrderBy(v => v.Span)
                .ThenBy(v => v.Residues, comparer);
            result.AddRange(kept);
        }

        return result;
    }

    private static void Search(
        int currentPitch,
        List<int> residues,
        List<int> intervals,
        HashSet<int> used,
        IReadOnlyList<int> placement,
        int ktet,
        int runningPenalty,
        List<Voicing> output)
    {
        if (used.Count == placement.Count)
        {
            output.Add(new Voicing(
                Root: residues[0],
                Residues: residues.ToArray(),
                Intervals: intervals.ToArray(),
                Span: intervals.Sum(),
                Penalty: runningPenalty));
            return;
        }

        var currentResidue = ((currentPitch % ktet) + ktet) % ktet;
        foreach (var p in placement)
        {
            if (used.Contains(p)) continue;
            var delta = ((p - currentResidue) % ktet + ktet) % ktet;
            if (delta == 0 || delta == 1) continue;

            var next = currentPitch + delta;
            residues.Add(p);
            intervals.Add(delta);
            used.Add(p);

            Search(next, residues, intervals, used, placement, ktet, runningPenalty + Penalty(delta), output);

            residues.RemoveAt(residues.Count - 1);
            intervals.RemoveAt(intervals.Count - 1);
            used.Remove(p);
        }
    }

    private sealed class ResidueLexComparer : IComparer<IReadOnlyList<int>>
    {
        public int Compare(IReadOnlyList<int>? x, IReadOnlyList<int>? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            var n = Math.Min(x.Count, y.Count);
            for (var i = 0; i < n; i++)
            {
                var c = x[i].CompareTo(y[i]);
                if (c != 0) return c;
            }
            return x.Count.CompareTo(y.Count);
        }
    }
}
