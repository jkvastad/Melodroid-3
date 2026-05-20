using FluentAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class GoodFractionsTests
{
    [Fact]
    public void Enumerate_4_3_returns_hand_verified_set_in_order()
    {
        var result = GoodFractions.Enumerate(maxSize: 4, maxPrime: 3);

        result.Should().Equal(
            new Fraction(1, 1),
            new Fraction(4, 3),
            new Fraction(3, 2));
    }

    [Fact]
    public void Enumerate_24_5_contains_known_just_ratios()
    {        
        var result = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);
        result.Should().Contain(new Fraction(1, 1));
        result.Should().Contain(new Fraction(16, 15));
        result.Should().Contain(new Fraction(10, 9));
        result.Should().Contain(new Fraction(9, 8));
        result.Should().Contain(new Fraction(6, 5));
        result.Should().Contain(new Fraction(5, 4));
        result.Should().Contain(new Fraction(4, 3));
        result.Should().Contain(new Fraction(3, 2));
        result.Should().Contain(new Fraction(8, 5));
        result.Should().Contain(new Fraction(5, 3));
        result.Should().Contain(new Fraction(16, 9));
        result.Should().Contain(new Fraction(9, 5));
        result.Should().Contain(new Fraction(15, 8));
    }

    [Fact]
    public void Enumerate_24_5_excludes_non_smooth_ratios()
    {
        var result = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);

        // 7 is not 5-smooth, so any fraction containing 7 must be absent.
        result.Should().NotContain(new Fraction(8, 7));
        result.Should().NotContain(new Fraction(7, 5));
        result.Should().NotContain(new Fraction(7, 4));
    }

    [Theory]
    [InlineData(24, 5)]
    [InlineData(24, 3)]
    [InlineData(15, 5)]
    [InlineData(4, 3)]
    public void All_returned_fractions_satisfy_constraints(int maxSize, int maxPrime)
    {
        var result = GoodFractions.Enumerate(maxSize, maxPrime);

        foreach (var f in result)
        {
            f.Value.Should().BeInRange(1.0, double.MaxValue);
            f.Value.Should().BeLessThan(2.0);
            f.Numerator.Should().BeLessThanOrEqualTo(maxSize);
            f.Denominator.Should().BeLessThanOrEqualTo(maxSize);
            Gcd(f.Numerator, f.Denominator).Should().Be(1);
            IsSmooth(f.Numerator, maxPrime).Should().BeTrue();
            IsSmooth(f.Denominator, maxPrime).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(24, 5)]
    [InlineData(15, 3)]
    [InlineData(4, 3)]
    public void Result_is_strictly_ascending_by_value(int maxSize, int maxPrime)
    {
        var values = GoodFractions.Enumerate(maxSize, maxPrime).Select(f => f.Value).ToArray();

        for (var i = 1; i < values.Length; i++)
        {
            values[i].Should().BeGreaterThan(values[i - 1]);
        }
    }

    [Fact]
    public void Enumerate_1_5_returns_only_unison()
    {
        GoodFractions.Enumerate(maxSize: 1, maxPrime: 5)
            .Should().Equal(new Fraction(1, 1));
    }

    [Fact]
    public void Enumerate_24_2_returns_only_unison_since_no_other_powers_of_two_fit_half_open_octave()
    {
        // Powers of 2 up to 24: 1, 2, 4, 8, 16. Coprime p/q in [1, 2) with both p,q powers of 2
        // requires gcd(p,q)=1, which forces q=1 → p in [1, 2) → p=1.
        GoodFractions.Enumerate(maxSize: 24, maxPrime: 2)
            .Should().Equal(new Fraction(1, 1));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(24, 1)]
    [InlineData(24, 0)]
    public void Edge_inputs_return_unison_without_throwing(int maxSize, int maxPrime)
    {
        var act = () => GoodFractions.Enumerate(maxSize, maxPrime);

        act.Should().NotThrow();
        act().Should().Equal(new Fraction(1, 1));
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }

    private static bool IsSmooth(int n, int maxPrime)
    {
        for (var p = 2; p <= maxPrime; p++)
        {
            if (!IsPrime(p)) continue;
            while (n % p == 0) n /= p;
        }
        return n == 1;
    }

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        for (var i = 2; i * i <= n; i++) if (n % i == 0) return false;
        return true;
    }
}
