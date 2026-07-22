using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class ChordsTests
{
    private const int Ktet = 12;

    // Known count of binary necklaces of length 12 with s ones (the independent oracle):
    // one representative per transposition class of a size-s chord in 12-tet.
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 6)]
    [InlineData(3, 19)]
    [InlineData(4, 43)]
    [InlineData(5, 66)]
    [InlineData(6, 80)]
    [InlineData(7, 66)]
    [InlineData(8, 43)]
    [InlineData(9, 19)]
    [InlineData(10, 6)]
    [InlineData(11, 1)]
    [InlineData(12, 1)]
    public void Enumerate_matches_the_necklace_count_per_size(int size, int expectedCount)
    {
        var (chords, _) = Chords.Enumerate(Ktet, size, size, maxResults: 100000);

        chords.Should().HaveCount(expectedCount);
        chords.Should().OnlyContain(c => c.Keys.Count == size);
    }

    [Fact]
    public void Enumerate_default_2_to_7_totals_280()
    {
        // 6 + 19 + 43 + 66 + 80 + 66 = 280.
        var (chords, truncated) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000);

        chords.Should().HaveCount(280);
        truncated.Should().BeFalse();
    }

    [Fact]
    public void Enumerate_emits_the_major_triad_once_and_not_its_transpositions()
    {
        // 0 4 7, 0 3 8 and 0 5 9 are the same class; the canonical (most-compact) form is 0 4 7.
        var (chords, _) = Chords.Enumerate(Ktet, 3, 3, maxResults: 100000);

        chords.Should().ContainSingle(c => c.Keys.SequenceEqual(new[] { 0, 4, 7 }));
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 3, 8 }));
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 5, 9 }));
    }

    [Fact]
    public void Enumerate_every_chord_is_canonical_starting_at_zero_and_sorted()
    {
        var (chords, _) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000);

        chords.Should().OnlyContain(c => c.Keys[0] == 0);
        chords.Should().OnlyContain(c => c.Keys.SequenceEqual(c.Keys.OrderBy(k => k)));
    }

    [Fact]
    public void Enumerate_intervals_sum_to_ktet()
    {
        var (chords, _) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000);

        chords.Should().OnlyContain(c => c.Intervals.Sum() == Ktet);
    }

    // Symmetric chords have small orbits: their stabilizer under transposition is non-trivial.
    [Theory]
    [InlineData(new[] { 0, 4, 8 }, 4)]        // augmented triad
    [InlineData(new[] { 0, 3, 6, 9 }, 3)]     // diminished 7th
    [InlineData(new[] { 0, 2, 4, 6, 8, 10 }, 2)] // whole-tone scale
    [InlineData(new[] { 0, 4, 7 }, 12)]       // asymmetric major triad — full orbit
    public void Enumerate_reports_the_orbit_size_of_a_symmetric_chord(int[] keys, int expectedOrbit)
    {
        var (chords, _) = Chords.Enumerate(Ktet, keys.Length, keys.Length, maxResults: 100000);

        var chord = chords.Single(c => c.Keys.SequenceEqual(keys));
        chord.OrbitSize.Should().Be(expectedOrbit);
    }

    [Fact]
    public void Enumerate_orbit_sizes_of_a_size_sum_to_the_transposition_count()
    {
        // The sum of orbit sizes over all classes of a given size equals the number of raw
        // size-s subsets: C(12, s). For s = 3 that is 220.
        var (chords, _) = Chords.Enumerate(Ktet, 3, 3, maxResults: 100000);

        chords.Sum(c => c.OrbitSize).Should().Be(220);
    }

    [Fact]
    public void Enumerate_max_results_truncates_and_flags()
    {
        var (chords, truncated) = Chords.Enumerate(Ktet, 2, 7, maxResults: 10);

        chords.Should().HaveCount(10);
        truncated.Should().BeTrue();
    }

    [Fact]
    public void Enumerate_is_sorted_by_size_then_keys()
    {
        var (chords, _) = Chords.Enumerate(Ktet, 2, 4, maxResults: 100000);

        for (var i = 1; i < chords.Count; i++)
        {
            var prev = chords[i - 1];
            var cur = chords[i];
            var order = prev.Keys.Count.CompareTo(cur.Keys.Count);
            if (order == 0)
            {
                order = Lexicographic(prev.Keys, cur.Keys);
            }
            order.Should().BeLessThanOrEqualTo(0);
        }
    }

    [Fact]
    public void Enumerate_no_minor_seconds_drops_the_semitone_dyad()
    {
        // The minor-second / major-seventh dyad is one necklace (canonically 0 1). The pure filter
        // drops every chord with a semitone, so neither voicing survives without --allow-maj7.
        var (chords, _) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000, excludeMinorSeconds: true);

        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1 }));
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 11 }));
    }

    [Fact]
    public void Enumerate_no_minor_seconds_drops_chords_with_an_internal_minor_second()
    {
        var (chords, _) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000, excludeMinorSeconds: true);

        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1, 2 }));
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1, 6 }));
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 2, 3 }));
    }

    [Fact]
    public void Enumerate_no_minor_seconds_keeps_semitone_free_chords()
    {
        var (chords, _) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000, excludeMinorSeconds: true);

        chords.Should().Contain(c => c.Keys.SequenceEqual(new[] { 0, 4, 7 })); // major triad
        chords.Should().Contain(c => c.Keys.SequenceEqual(new[] { 0, 4, 8 })); // augmented triad
    }

    [Fact]
    public void Enumerate_no_minor_seconds_keeps_only_five_dyads()
    {
        // Six interval classes exist; the semitone class (0 1) is dropped by the pure filter,
        // leaving five. Neither 0 1 nor its 0 11 voicing survives.
        var (chords, _) = Chords.Enumerate(Ktet, 2, 2, maxResults: 100000, excludeMinorSeconds: true);

        chords.Should().HaveCount(5);
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1 }));
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 11 }));
    }

    [Fact]
    public void Enumerate_no_minor_seconds_leaves_no_semitone()
    {
        var (chords, _) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000, excludeMinorSeconds: true);

        foreach (var chord in chords)
        {
            var keys = chord.Keys;
            // Independent oracle: recompute the cyclic semitone gaps straight from Keys.
            var hasSemitone = false;
            for (var i = 0; i < keys.Count; i++)
            {
                var next = i + 1 < keys.Count ? keys[i + 1] : keys[0] + Ktet;
                if (next - keys[i] == 1) { hasSemitone = true; break; }
            }

            hasSemitone.Should().BeFalse(
                "chord {0} keeps a minor second under the pure filter", string.Join(" ", keys));
        }
    }

    [Fact]
    public void Enumerate_no_minor_seconds_stays_sorted_by_size_then_keys()
    {
        // The pure filter only drops chords, so generation order (size then keys) is preserved.
        var (chords, _) = Chords.Enumerate(Ktet, 2, 5, maxResults: 100000, excludeMinorSeconds: true);

        AssertSortedBySizeThenKeys(chords);
    }

    [Fact]
    public void Enumerate_allow_maj7_includes_the_major_seventh_chord()
    {
        // The maj7 necklace canonicalises to 0 1 5 8 (internal semitone); --allow-maj7 re-voices its
        // single semitone to the wrap, so it appears as 0 4 7 11 rather than being dropped.
        var (chords, _) = Chords.Enumerate(
            Ktet, 2, 7, maxResults: 100000, excludeMinorSeconds: true, allowMajorSevenths: true);

        chords.Should().ContainSingle(c => c.Keys.SequenceEqual(new[] { 0, 4, 7, 11 }));
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1, 5, 8 }));
    }

    [Fact]
    public void Enumerate_allow_maj7_revoices_the_major_seventh_dyad()
    {
        var (chords, _) = Chords.Enumerate(
            Ktet, 2, 7, maxResults: 100000, excludeMinorSeconds: true, allowMajorSevenths: true);

        chords.Should().ContainSingle(c => c.Keys.SequenceEqual(new[] { 0, 11 }));
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1 }));
    }

    [Fact]
    public void Enumerate_allow_maj7_keeps_all_six_dyads()
    {
        // Every dyad has at most one semitone, so all six interval classes survive; the semitone
        // class is re-voiced 0 1 → 0 11, the rest are untouched.
        var (chords, _) = Chords.Enumerate(
            Ktet, 2, 2, maxResults: 100000, excludeMinorSeconds: true, allowMajorSevenths: true);

        chords.Should().HaveCount(6);
        chords.Should().ContainSingle(c => c.Keys.SequenceEqual(new[] { 0, 11 }));
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1 }));
    }

    [Fact]
    public void Enumerate_allow_maj7_revoices_a_single_semitone_chord_to_the_wrap()
    {
        // 0 1 6 has one semitone; its upper note (1) becomes the root, re-voicing it to 0 5 11.
        var (chords, _) = Chords.Enumerate(
            Ktet, 2, 7, maxResults: 100000, excludeMinorSeconds: true, allowMajorSevenths: true);

        chords.Should().ContainSingle(c => c.Keys.SequenceEqual(new[] { 0, 5, 11 }));
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1, 6 }));
    }

    [Fact]
    public void Enumerate_allow_maj7_drops_chords_with_two_or_more_semitones()
    {
        var (chords, _) = Chords.Enumerate(
            Ktet, 2, 7, maxResults: 100000, excludeMinorSeconds: true, allowMajorSevenths: true);

        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1, 2 }));    // gaps 1 1 10
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1, 2, 6 })); // two semitones
    }

    [Fact]
    public void Enumerate_allow_maj7_leaves_no_internal_semitone()
    {
        var (chords, _) = Chords.Enumerate(
            Ktet, 2, 7, maxResults: 100000, excludeMinorSeconds: true, allowMajorSevenths: true);

        foreach (var chord in chords)
        {
            var keys = chord.Keys;
            // Independent oracle: only the wrap gap (top note → octave) may be a semitone.
            var hasInternalSemitone = false;
            for (var i = 0; i + 1 < keys.Count; i++)
                if (keys[i + 1] - keys[i] == 1) { hasInternalSemitone = true; break; }

            hasInternalSemitone.Should().BeFalse(
                "chord {0} keeps an internal minor second", string.Join(" ", keys));
        }
    }

    [Fact]
    public void Enumerate_allow_maj7_stays_sorted_by_size_then_keys()
    {
        // Re-voicing single-semitone chords moves them out of generation order; must still be sorted.
        var (chords, _) = Chords.Enumerate(
            Ktet, 2, 5, maxResults: 100000, excludeMinorSeconds: true, allowMajorSevenths: true);

        AssertSortedBySizeThenKeys(chords);
    }

    [Fact]
    public void Enumerate_no_tritones_drops_the_tritone_dyad()
    {
        var (chords, _) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000, excludeTritones: true);

        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 6 }));
    }

    [Fact]
    public void Enumerate_no_tritones_drops_chords_whose_tritone_is_not_an_adjacent_gap()
    {
        // The tritones in these chords sit between non-adjacent notes (0 3 6 9: 0–6 and 3–9;
        // 0 4 6 10: 0–6 and 4–10), so an adjacent-gap filter would miss them. The all-pairs filter
        // drops them.
        var (chords, _) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000, excludeTritones: true);

        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 3, 6, 9 }));  // diminished 7th
        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 4, 6, 10 }));
    }

    [Fact]
    public void Enumerate_no_tritones_keeps_tritone_free_chords()
    {
        var (chords, _) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000, excludeTritones: true);

        chords.Should().Contain(c => c.Keys.SequenceEqual(new[] { 0, 4, 7 })); // major triad
        chords.Should().Contain(c => c.Keys.SequenceEqual(new[] { 0, 4, 8 })); // augmented triad
    }

    [Fact]
    public void Enumerate_no_tritones_leaves_no_tritone()
    {
        var (chords, _) = Chords.Enumerate(Ktet, 2, 7, maxResults: 100000, excludeTritones: true);

        foreach (var chord in chords)
        {
            var keys = chord.Keys;
            // Independent oracle: recompute the circular interval of every pair straight from Keys.
            var hasTritone = false;
            for (var i = 0; i < keys.Count && !hasTritone; i++)
                for (var j = i + 1; j < keys.Count; j++)
                {
                    var d = keys[j] - keys[i];
                    if (Math.Min(d, Ktet - d) == 6) { hasTritone = true; break; }
                }

            hasTritone.Should().BeFalse(
                "chord {0} keeps a tritone", string.Join(" ", keys));
        }
    }

    [Fact]
    public void Enumerate_no_tritones_combines_with_no_minor_seconds()
    {
        // 0 1 6 has both a semitone (0–1) and a tritone (0–6); with both filters it is dropped,
        // while a chord free of both survives.
        var (chords, _) = Chords.Enumerate(
            Ktet, 2, 7, maxResults: 100000, excludeMinorSeconds: true, excludeTritones: true);

        chords.Should().NotContain(c => c.Keys.SequenceEqual(new[] { 0, 1, 6 }));
        chords.Should().Contain(c => c.Keys.SequenceEqual(new[] { 0, 4, 7 }));
    }

    private static void AssertSortedBySizeThenKeys(IReadOnlyList<Chord> chords)
    {
        for (var i = 1; i < chords.Count; i++)
        {
            var prev = chords[i - 1];
            var cur = chords[i];
            var order = prev.Keys.Count.CompareTo(cur.Keys.Count);
            if (order == 0)
            {
                order = Lexicographic(prev.Keys, cur.Keys);
            }
            order.Should().BeLessThanOrEqualTo(0);
        }
    }

    private static int Lexicographic(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            var c = a[i].CompareTo(b[i]);
            if (c != 0) return c;
        }
        return a.Count.CompareTo(b.Count);
    }
}
