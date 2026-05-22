using FluentAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class OctaveSweepTests
{
    private static readonly IReadOnlyList<Fraction> DefaultGoodFractions =
        GoodFractions.Enumerate(24, 5);

    private const double Unambiguous = 1.0 / 161.0;

    [Fact]
    public void Reference_equal_to_an_input_ratio_makes_that_input_bin_to_1_over_1()
    {
        var rows = OctaveSweep.Compute(
            new[] { 1.0 },
            DefaultGoodFractions,
            sweepStep: 0.5,
            binRadius: Unambiguous);

        var first = rows[0];
        first.ReferenceRatio.Should().Be(1.0);
        first.Cells[0].GoodFraction.Should().Be(new Fraction(1, 1));
        first.Cells[0].SignedPctDistance.Should().BeApproximately(0.0, 1e-9);
        first.FullMatch.Should().BeTrue();
        first.PostBinLcm.Should().Be(1);
    }

    [Fact]
    public void Major_triad_at_reference_1_produces_full_match()
    {
        var rows = OctaveSweep.Compute(
            new[] { 1.0, 1.25, 1.5 },
            DefaultGoodFractions,
            sweepStep: 0.5,
            binRadius: Unambiguous);

        var row = rows.Single(r => r.ReferenceRatio == 1.0);
        row.FullMatch.Should().BeTrue();
        row.Ambiguous.Should().BeFalse();
        row.PostBinLcm.Should().Be(4);
        row.Cells.Select(c => c.GoodFraction).Should().Equal(
            new Fraction(1, 1),
            new Fraction(5, 4),
            new Fraction(3, 2));
        foreach (var c in row.Cells)
        {
            c.SignedPctDistance.Should().BeApproximately(0.0, 1e-9);
        }
    }

    [Fact]
    public void Major_triad_renormalized_by_5_4_yields_minor_chord_with_lcm_5()
    {
        var step = 0.001;
        var rows = OctaveSweep.Compute(
            new[] { 1.0, 1.25, 1.5 },
            DefaultGoodFractions,
            sweepStep: step,
            binRadius: Unambiguous);

        var row = rows.Single(r => Math.Abs(r.ReferenceRatio - 1.25) < step / 2);
        row.FullMatch.Should().BeTrue();
        row.PostBinLcm.Should().Be(5);

        var gfs = row.Cells.Select(c => c.GoodFraction).OrderBy(f => f.Value).ToArray();
        gfs.Should().Equal(
            new Fraction(1, 1),
            new Fraction(6, 5),
            new Fraction(8, 5));
    }

    [Fact]
    public void Overlapping_bin_radius_marks_row_ambiguous()
    {
        var rows = OctaveSweep.Compute(
            new[] { 1.075 },
            DefaultGoodFractions,
            sweepStep: 0.5,
            binRadius: 0.05);

        var row = rows.Single(r => r.ReferenceRatio == 1.0);
        row.Ambiguous.Should().BeTrue();
        row.PostBinLcm.Should().BeNull();
        row.FullMatch.Should().BeFalse();
    }

    [Fact]
    public void Ratio_outside_any_bin_excluded_from_full_match_but_others_contribute_to_lcm()
    {
        // 1.035 sits in the gap between 1/1's bin ([0.9938, 1.0062]) and 16/15's bin
        // ([1.0601, 1.0733]) at the unambiguous radius, so it must remain unbinned.
        var unbindable = 1.035;
        var rows = OctaveSweep.Compute(
            new[] { 1.0, 1.5, unbindable },
            DefaultGoodFractions,
            sweepStep: 0.5,
            binRadius: Unambiguous);

        var row = rows.Single(r => r.ReferenceRatio == 1.0);
        row.FullMatch.Should().BeFalse();
        row.Ambiguous.Should().BeFalse();
        row.PostBinLcm.Should().Be(IntegerMath.Lcm(1, 2));

        row.Cells[2].GoodFraction.Should().Be(default(Fraction));
        double.IsNaN(row.Cells[2].SignedPctDistance).Should().BeTrue();
    }

    [Fact]
    public void Sweep_step_controls_row_count()
    {
        var rows = OctaveSweep.Compute(
            new[] { 1.0 },
            DefaultGoodFractions,
            sweepStep: 0.01,
            binRadius: Unambiguous);

        rows.Should().HaveCount(100);
        rows[0].ReferenceRatio.Should().BeApproximately(1.0, 1e-12);
        rows[^1].ReferenceRatio.Should().BeApproximately(1.99, 1e-12);
    }
}
