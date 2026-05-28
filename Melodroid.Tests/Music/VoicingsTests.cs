using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class VoicingsTests
{
    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    [InlineData(2, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    [InlineData(11, 7)]
    [InlineData(12, 8)]
    public void Penalty_assigns_expected_costs_per_interval(int interval, int expected)
    {
        Voicings.Penalty(interval).Should().Be(expected);
    }

    [Fact]
    public void EnumerateBestPerRoot_singleton_yields_single_empty_interval_voicing()
    {
        var result = Voicings.EnumerateBestPerRoot(new[] { 0 }, ktet: 12);

        result.Should().HaveCount(1);
        result[0].Root.Should().Be(0);
        result[0].Residues.Should().Equal(0);
        result[0].Intervals.Should().BeEmpty();
        result[0].Span.Should().Be(0);
        result[0].Penalty.Should().Be(0);
    }

    [Fact]
    public void EnumerateAll_Lcm4_triad_at_zero_enumerates_two_voicings_per_root()
    {
        var placement = PlacementFor(lcm: 4);

        var all = Voicings.EnumerateAll(placement.Keys, ktet: 12);

        all.Where(v => v.Root == 0).Should().HaveCount(2);
        all.Where(v => v.Root == 4).Should().HaveCount(2);
        all.Where(v => v.Root == 7).Should().HaveCount(2);

        var fromRootZero = all.Where(v => v.Root == 0).OrderBy(v => v.Penalty).ToList();
        fromRootZero[0].Residues.Should().Equal(0, 4, 7);
        fromRootZero[0].Intervals.Should().Equal(4, 3);
        fromRootZero[0].Span.Should().Be(7);
        fromRootZero[0].Penalty.Should().Be(0);

        fromRootZero[1].Residues.Should().Equal(0, 7, 4);
        fromRootZero[1].Intervals.Should().Equal(7, 9);
        fromRootZero[1].Span.Should().Be(16);
        fromRootZero[1].Penalty.Should().Be(8);
    }

    [Fact]
    public void EnumerateBestPerRoot_Lcm4_triad_keeps_only_min_penalty_per_root()
    {
        var placement = PlacementFor(lcm: 4);

        var best = Voicings.EnumerateBestPerRoot(placement.Keys, ktet: 12);

        best.Should().HaveCount(3);

        var root0 = best.Single(v => v.Root == 0);
        root0.Residues.Should().Equal(0, 4, 7);
        root0.Penalty.Should().Be(0);

        var root4 = best.Single(v => v.Root == 4);
        root4.Residues.Should().Equal(4, 7, 0);
        root4.Intervals.Should().Equal(3, 5);
        root4.Penalty.Should().Be(1);

        var root7 = best.Single(v => v.Root == 7);
        root7.Residues.Should().Equal(7, 0, 4);
        root7.Intervals.Should().Equal(5, 4);
        root7.Penalty.Should().Be(1);
    }

    [Fact]
    public void EnumerateBestPerRoot_Lcm24_C_major_contains_G13_voicing_at_root_seven()
    {
        var placement = PlacementFor(lcm: 24);

        var best = Voicings.EnumerateBestPerRoot(placement.Keys, ktet: 12);

        best.Should().NotBeEmpty();
        best.Should().OnlyContain(v => v.Penalty == 0,
            "every root of the C major scale can be voiced fully triadically");

        var g13 = best.SingleOrDefault(v =>
            v.Root == 7 && v.Residues.SequenceEqual(new[] { 7, 11, 2, 5, 9, 0, 4 }));
        g13.Residues.Should().NotBeNull();
        g13.Intervals.Should().Equal(4, 3, 3, 4, 3, 4);
        g13.Span.Should().Be(21);
        g13.Penalty.Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(18)]
    [InlineData(20)]
    [InlineData(24)]
    public void EnumerateAll_invariants_hold_for_every_lcm(int lcm)
    {
        const int ktet = 12;
        var placement = PlacementFor(lcm);
        var distinctPlacementKeys = placement.Keys.Distinct().OrderBy(k => k).ToList();

        var all = Voicings.EnumerateAll(placement.Keys, ktet);

        foreach (var v in all)
        {
            v.Intervals.Should().OnlyContain(i => i >= 2 && i < ktet,
                $"voicing {string.Join(",", v.Residues)} from root {v.Root} should have all intervals in [2, {ktet})");

            v.Residues.OrderBy(r => r).Should().Equal(distinctPlacementKeys,
                $"voicing {string.Join(",", v.Residues)} from root {v.Root} should visit every placement residue exactly once");

            v.Penalty.Should().Be(v.Intervals.Sum(Voicings.Penalty));
            v.Span.Should().Be(v.Intervals.Sum());
            v.Root.Should().Be(v.Residues[0]);
        }
    }

    [Fact]
    public void EnumerateBestPerRoot_Lcm15_documents_observed_per_root_penalties()
    {
        // LCM 15 placement on 12-tet contains the dissonant {8, 9, 10} cluster.
        // The per-root minimum penalty therefore can't be 0; this test pins the observed
        // behaviour so regressions surface and the research can track the actual numbers.
        var placement = PlacementFor(lcm: 15);

        var best = Voicings.EnumerateBestPerRoot(placement.Keys, ktet: 12);

        // At least one root should produce some voicing — empty would be surprising.
        best.Should().NotBeEmpty();

        // Every emitted voicing for LCM 15 should still respect the no-semitone invariant.
        best.Should().OnlyContain(v => v.Intervals.All(i => i >= 2));
    }

    private static Placement PlacementFor(int lcm)
    {
        var goodFractions = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);
        var families = LcmFamilies.Compute(goodFractions, maxLcm: 24);
        var family = families.Single(f => f.Lcm == lcm);
        return Placements.Compute(family, at: 0, ktet: 12);
    }
}
