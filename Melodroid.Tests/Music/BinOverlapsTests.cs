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
        // (3/2 - 1) / (3/2 + 1) = (1/2) / (5/2) = 1/5
        result[0].Radius.Should().Be(new Fraction(1, 5));

        result[1].Lower.Should().Be(new Fraction(3, 2));
        result[1].Upper.Should().Be(new Fraction(2, 1));
        // (2 - 3/2) / (2 + 3/2) = (1/2) / (7/2) = 1/7
        result[1].Radius.Should().Be(new Fraction(1, 7));
    }

    [Fact]
    public void Compute_radius_is_returned_in_reduced_form()
    {
        // 10/9 → 9/8: (9/8 - 10/9)/(9/8 + 10/9) = (1/72)/(161/72) = 1/161.
        // Pre-reduction the numerator/denominator would be (9*9 - 10*8, 9*9 + 10*8) = (1, 161),
        // already coprime — but pick a case where reduction matters too.
        var coprime = BinOverlaps.Compute(new[] { new Fraction(10, 9), new Fraction(9, 8) });
        coprime[0].Radius.Should().Be(new Fraction(1, 161));

        // 1/1 → 9/8: (9 - 8)/(9 + 8) = 1/17, already coprime.
        var ones = BinOverlaps.Compute(new[] { new Fraction(1, 1), new Fraction(9, 8) });
        ones[0].Radius.Should().Be(new Fraction(1, 17));
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
        result[0].Radius.Should().Be(new Fraction(1, 3));
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
            overlaps[i].Lower.Should().Be(fractions[i]);
            overlaps[i].Upper.Should().Be(fractions[i + 1]);
            overlaps[i].Radius.Should().Be(ExpectedRadius(fractions[i], fractions[i + 1]));
        }

        overlaps[^1].Lower.Should().Be(fractions[^1]);
        overlaps[^1].Upper.Should().Be(new Fraction(2, 1));
        overlaps[^1].Radius.Should().Be(ExpectedRadius(fractions[^1], new Fraction(2, 1)));
    }

    private static Fraction ExpectedRadius(Fraction a, Fraction b)
    {
        var num = b.Numerator * a.Denominator - a.Numerator * b.Denominator;
        var den = b.Numerator * a.Denominator + a.Numerator * b.Denominator;
        var g = Gcd(num, den);
        return new Fraction(num / g, den / g);
    }

    private static int Gcd(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0) (a, b) = (b, a % b);
        return a == 0 ? 1 : a;
    }
}
