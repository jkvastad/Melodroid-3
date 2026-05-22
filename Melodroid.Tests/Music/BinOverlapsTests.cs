using FluentAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class BinOverlapsTests
{
    [Fact]
    public void Compute_simple_pair_uses_formula_b_minus_a_over_b_plus_a()
    {
        var input = new[] { new Fraction(1, 1), new Fraction(3, 2) };
        var result = BinOverlaps.Compute(input);

        result.Should().HaveCount(2);

        result[0].Lower.Should().Be(new Fraction(1, 1));
        result[0].Upper.Should().Be(new Fraction(3, 2));
        result[0].Radius.Should().BeApproximately((1.5 - 1.0) / (1.5 + 1.0), 1e-12);

        result[1].Lower.Should().Be(new Fraction(3, 2));
        result[1].Upper.Should().Be(new Fraction(2, 1));
        result[1].Radius.Should().BeApproximately((2.0 - 1.5) / (2.0 + 1.5), 1e-12);
    }

    [Fact]
    public void Compute_wrap_row_upper_is_always_two_over_one()
    {
        var input = new[] { new Fraction(1, 1), new Fraction(9, 8), new Fraction(15, 8) };
        var result = BinOverlaps.Compute(input);

        result[^1].Upper.Should().Be(new Fraction(2, 1));
        result[^1].Lower.Should().Be(new Fraction(15, 8));
    }

    [Fact]
    public void Compute_single_fraction_returns_only_wrap_row()
    {
        var result = BinOverlaps.Compute(new[] { new Fraction(1, 1) });

        result.Should().HaveCount(1);
        result[0].Lower.Should().Be(new Fraction(1, 1));
        result[0].Upper.Should().Be(new Fraction(2, 1));
        result[0].Radius.Should().BeApproximately(1.0 / 3.0, 1e-12);
    }

    [Fact]
    public void Compute_empty_returns_empty()
    {
        BinOverlaps.Compute(Array.Empty<Fraction>())
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(24, 5)]
    [InlineData(12, 3)]
    [InlineData(4, 3)]
    public void Each_row_radius_matches_independent_formula(int maxSize, int maxPrime)
    {
        var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
        var overlaps = BinOverlaps.Compute(fractions);

        overlaps.Should().HaveCount(fractions.Count);

        for (var i = 0; i < fractions.Count - 1; i++)
        {
            var a = fractions[i].Value;
            var b = fractions[i + 1].Value;
            var expected = (b - a) / (b + a);
            overlaps[i].Lower.Should().Be(fractions[i]);
            overlaps[i].Upper.Should().Be(fractions[i + 1]);
            overlaps[i].Radius.Should().BeApproximately(expected, 1e-12);
        }

        var last = fractions[^1].Value;
        var expectedWrap = (2.0 - last) / (2.0 + last);
        overlaps[^1].Lower.Should().Be(fractions[^1]);
        overlaps[^1].Upper.Should().Be(new Fraction(2, 1));
        overlaps[^1].Radius.Should().BeApproximately(expectedWrap, 1e-12);
    }
}
