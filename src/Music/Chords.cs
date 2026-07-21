namespace Melodroid_3.Music;

public readonly record struct Chord(
    IReadOnlyList<int> Keys,       // canonical pitch classes, sorted ascending, starts at 0
    IReadOnlyList<int> Intervals,  // gaps around the octave (wrap-around); sum == ktet
    int OrbitSize);                // # distinct transpositions (ktet / stabilizer size)

public static class Chords
{
    /// <summary>
    /// Enumerates one representative per transposition class ("necklace") of a k-tet keyboard,
    /// for sizes in [minNotes, maxNotes]. Two chords are equivalent when one is a transposition
    /// of the other; the canonical representative is the most-compact rotation (smallest span from
    /// its lowest to highest key, ties broken lexicographically), transposed to start at 0 — the
    /// "normal order" of music set theory, e.g. the major triad canonicalises to 0 4 7. Results are
    /// sorted by size then keys lexicographically; <paramref name="maxResults"/> caps the rows
    /// returned (sets Truncated).
    /// </summary>
    public static (IReadOnlyList<Chord> Chords, bool Truncated) Enumerate(
        int ktet, int minNotes, int maxNotes, int maxResults)
    {
        var chords = new List<Chord>();
        if (ktet < 1 || minNotes < 1 || maxNotes < minNotes || maxResults < 1)
            return (chords, false);

        var truncated = false;
        for (var size = minNotes; size <= Math.Min(maxNotes, ktet); size++)
        {
            // A canonical chord always contains key 0, so choose 0 plus (size-1) of the remaining
            // keys 1..ktet-1. Keep only those equal to their own min-rotation (one per class).
            foreach (var rest in Combinations(ktet - 1, size - 1))
            {
                var keys = new int[size];
                keys[0] = 0;
                for (var i = 0; i < rest.Length; i++) keys[i + 1] = rest[i] + 1; // shift into 1..ktet-1

                if (!IsCanonical(keys, ktet)) continue;

                if (chords.Count >= maxResults) { truncated = true; return (chords, truncated); }
                chords.Add(new Chord(keys, Intervals(keys, ktet), OrbitSize(keys, ktet)));
            }
        }

        return (chords, truncated);
    }

    // True iff the (sorted, 0-containing) set is the canonical representative of its class:
    // no rotation, transposed to start at 0, is smaller under (span, then lexicographic keys).
    private static bool IsCanonical(int[] sortedKeys, int ktet)
    {
        // Only transpositions that map some member onto 0 start with 0 and can compete.
        foreach (var member in sortedKeys)
        {
            if (member == 0) continue;
            // Rotate so 'member' becomes 0; compare against sortedKeys under (span, lex).
            if (CompareRotation(sortedKeys, member, ktet) < 0) return false;
        }
        return true;
    }

    // Rotates sorted((s - shift) mod ktet) and compares against sortedKeys, ordering by span
    // (largest key, since both start at 0) first, then lexicographically. Returns negative if the
    // rotation is the more-compact/smaller form, positive if larger, 0 if equal.
    private static int CompareRotation(int[] sortedKeys, int shift, int ktet)
    {
        var size = sortedKeys.Length;
        var rotated = new int[size];
        for (var i = 0; i < size; i++) rotated[i] = ((sortedKeys[i] - shift) % ktet + ktet) % ktet;
        Array.Sort(rotated);

        var bySpan = rotated[size - 1].CompareTo(sortedKeys[size - 1]);
        if (bySpan != 0) return bySpan;

        for (var i = 0; i < size; i++)
        {
            var c = rotated[i].CompareTo(sortedKeys[i]);
            if (c != 0) return c;
        }
        return 0;
    }

    // Number of distinct transpositions of the set = ktet / |stabilizer|, where the stabilizer is
    // the set of shifts n in [0, ktet) with (set + n) mod ktet == set.
    private static int OrbitSize(int[] sortedKeys, int ktet)
    {
        var members = new HashSet<int>(sortedKeys);
        var stabilizer = 0;
        for (var n = 0; n < ktet; n++)
        {
            var fixes = true;
            foreach (var key in sortedKeys)
            {
                if (!members.Contains((key + n) % ktet)) { fixes = false; break; }
            }
            if (fixes) stabilizer++;
        }
        return ktet / stabilizer;
    }

    // Consecutive gaps around the octave, wrapping the last member back to the first + ktet.
    private static int[] Intervals(int[] sortedKeys, int ktet)
    {
        var size = sortedKeys.Length;
        var gaps = new int[size];
        for (var i = 0; i < size; i++)
        {
            var next = i + 1 < size ? sortedKeys[i + 1] : sortedKeys[0] + ktet;
            gaps[i] = next - sortedKeys[i];
        }
        return gaps;
    }

    // All strictly-increasing size-k combinations of the values 0..n-1.
    private static IEnumerable<int[]> Combinations(int n, int k)
    {
        if (k == 0) { yield return Array.Empty<int>(); yield break; }
        if (k > n) yield break;

        var indices = new int[k];
        for (var i = 0; i < k; i++) indices[i] = i;

        while (true)
        {
            yield return (int[])indices.Clone();

            // Advance to the next combination in lexicographic order.
            var pos = k - 1;
            while (pos >= 0 && indices[pos] == n - k + pos) pos--;
            if (pos < 0) yield break;
            indices[pos]++;
            for (var i = pos + 1; i < k; i++) indices[i] = indices[i - 1] + 1;
        }
    }
}
