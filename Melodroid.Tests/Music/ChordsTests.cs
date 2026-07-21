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
