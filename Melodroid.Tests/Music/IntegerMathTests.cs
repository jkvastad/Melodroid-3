using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class IntegerMathTests
{
    [Theory]
    [InlineData(12, 18, 6)]
    [InlineData(17, 5, 1)]
    [InlineData(0, 7, 7)]
    [InlineData(7, 0, 7)]
    [InlineData(-12, 18, 6)]
    [InlineData(12, -18, 6)]
    public void Gcd_matches_independent_oracle(int a, int b, int expected)
    {
        IntegerMath.Gcd(a, b).Should().Be(expected);
    }

    [Fact]
    public void Gcd_of_zero_and_zero_is_one_not_zero()
    {
        IntegerMath.Gcd(0, 0).Should().Be(1);
    }

    [Theory]
    [InlineData(4, 6, 12)]
    [InlineData(3, 5, 15)]
    [InlineData(1, 7, 7)]
    [InlineData(12, 8, 24)]
    public void Lcm_matches_product_over_gcd(int a, int b, int expected)
    {
        IntegerMath.Lcm(a, b).Should().Be(expected);
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(3, 7)]
    [InlineData(8, 12)]
    [InlineData(15, 25)]
    public void Lcm_equals_a_times_b_over_gcd_for_positives(int a, int b)
    {
        IntegerMath.Lcm(a, b).Should().Be(a * b / Gcd(a, b));
    }

    private static int Gcd(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0) (a, b) = (b, a % b);
        return a == 0 ? 1 : a;
    }
}

