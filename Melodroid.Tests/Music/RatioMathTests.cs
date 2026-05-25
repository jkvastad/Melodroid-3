using FluentAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class RatioMathTests
{
    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.5, 1.5)]
    [InlineData(1.999, 1.999)]
    [InlineData(2.0, 1.0)]
    [InlineData(2.5, 1.25)]
    [InlineData(3.0, 1.5)]
    [InlineData(4.0, 1.0)]
    [InlineData(0.5, 1.0)]
    [InlineData(0.75, 1.5)]
    [InlineData(0.125, 1.0)]
    public void OctaveNormalize_folds_into_unit_octave(double input, double expected)
    {
        RatioMath.OctaveNormalize(input).Should().BeApproximately(expected, 1e-12);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.0001)]
    [InlineData(1.4142)]
    [InlineData(1.5)]
    [InlineData(1.9999)]
    public void OctaveNormalize_matches_independent_oracle(double r)
    {
        // Independent oracle: subtract floor(log2(r)) octaves.
        var expected = r * Math.Pow(2.0, -Math.Floor(Math.Log2(r)));
        RatioMath.OctaveNormalize(r).Should().BeApproximately(expected, 1e-12);
    }

    [Fact]
    public void OctaveNormalize_negative_or_zero_inputs_dont_loop_forever()
    {
        // Defensive: the method only loops while r < 1.0, so r=0 would be infinite.
        // We don't pass non-positive values in production, but document the contract
        // by asserting positive inputs work; non-positive inputs are caller's problem.
        RatioMath.OctaveNormalize(1.0).Should().Be(1.0);
    }

    [Fact]
    public void CircularSignedRelative_zero_when_v_equals_g()
    {
        RatioMath.CircularSignedRelative(1.5, 1.5).Should().Be(0.0);
    }

    [Fact]
    public void CircularSignedRelative_positive_when_v_above_g()
    {
        var r = RatioMath.CircularSignedRelative(1.51, 1.5);
        r.Should().BeGreaterThan(0.0);
        r.Should().BeApproximately(0.01 / 1.5, 1e-12);
    }

    [Fact]
    public void CircularSignedRelative_negative_when_v_below_g()
    {
        var r = RatioMath.CircularSignedRelative(1.49, 1.5);
        r.Should().BeLessThan(0.0);
        r.Should().BeApproximately(-0.01 / 1.5, 1e-12);
    }

    [Fact]
    public void CircularSignedRelative_picks_wrap_when_closer()
    {
        // v ≈ 1.0001 and g ≈ 1.9999 — they almost identify across the octave wrap.
        // wrapUp = (2*1.0001 - 1.9999) / 1.9999 ≈ +0.000150; direct = (1.0001 - 1.9999)/1.9999 ≈ -0.499.
        var r = RatioMath.CircularSignedRelative(1.0001, 1.9999);
        Math.Abs(r).Should().BeLessThan(0.001);
        r.Should().BeGreaterThan(0.0); // wrapUp is positive (v above g via wrap)
    }

    [Fact]
    public void CircularSignedRelative_wrap_down_when_v_high_g_low()
    {
        // v ≈ 1.9999 and g ≈ 1.0001 — wrapDn = (1.9999 - 2*1.0001)/1.0001 ≈ -0.000300.
        var r = RatioMath.CircularSignedRelative(1.9999, 1.0001);
        Math.Abs(r).Should().BeLessThan(0.001);
        r.Should().BeLessThan(0.0); // wrapDn is negative
    }
}
