using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class KeySweepTests
{
    private static readonly IReadOnlyList<Fraction> DefaultGoodFractions =
        GoodFractions.Enumerate(24, 5);

    private const double Unambiguous = 1.0 / 161.0;

    [Fact]
    public void Row_count_equals_k()
    {
        var twelve = KeySweep.Compute(
            new[] { 1.0 }, DefaultGoodFractions, k: 12, binRadius: Unambiguous);
        twelve.Should().HaveCount(12);

        var nineteen = KeySweep.Compute(
            new[] { 1.0 }, DefaultGoodFractions, k: 19, binRadius: Unambiguous);
        nineteen.Should().HaveCount(19);
    }

    [Fact]
    public void Key_indices_and_ratios_are_2_to_the_n_over_k_in_order()
    {
        const int k = 12;
        var rows = KeySweep.Compute(
            new[] { 1.0 }, DefaultGoodFractions, k, binRadius: Unambiguous);

        for (var n = 0; n < k; n++)
        {
            rows[n].KeyIndex.Should().Be(n);
            rows[n].KeyRatio.Should().BeApproximately(Math.Pow(2.0, (double)n / k), 1e-12);
        }
    }

    [Fact]
    public void Key0_with_major_triad_produces_full_match_to_1_5_over_4_3_over_2()
    {
        var rows = KeySweep.Compute(
            new[] { 1.0, 1.25, 1.5 }, DefaultGoodFractions, k: 12, binRadius: Unambiguous);

        var row = rows[0];
        row.KeyRatio.Should().Be(1.0);
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
    public void Default_radius_at_c_k_binds_every_good_fraction_input_at_some_key()
    {
        const int k = 12;
        var ck = KeysNeeded.WorstCaseForK(DefaultGoodFractions, k).Radius;

        foreach (var g in DefaultGoodFractions)
        {
            var rows = KeySweep.Compute(
                new[] { g.Value }, DefaultGoodFractions, k, binRadius: ck);

            rows.Any(r => r.AllInputsBinned).Should().BeTrue(
                $"good fraction {g} must bin at some key at binRadius = c_k");
        }
    }

    [Fact]
    public void Empty_inputs_or_zero_k_produces_empty_result()
    {
        KeySweep.Compute(Array.Empty<double>(), DefaultGoodFractions, k: 12, binRadius: Unambiguous)
            .Should().BeEmpty();
        KeySweep.Compute(new[] { 1.0 }, DefaultGoodFractions, k: 0, binRadius: Unambiguous)
            .Should().BeEmpty();
    }
}

