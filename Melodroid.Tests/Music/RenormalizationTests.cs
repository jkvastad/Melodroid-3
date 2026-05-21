using FluentAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class RenormalizationTests
{
    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(5, 3, 5, 3)]
    [InlineData(15, 8, 15, 8)]
    [InlineData(4, 5, 8, 5)]
    [InlineData(12, 10, 6, 5)]
    [InlineData(1, 4, 1, 1)]
    [InlineData(2, 1, 1, 1)]
    [InlineData(7, 2, 7, 4)]
    public void OctaveNormalize_brings_fraction_into_half_open_octave_and_simplifies(
        int inNum, int inDen, int expectedNum, int expectedDen)
    {
        var result = Renormalization.OctaveNormalize(new Fraction(inNum, inDen));

        result.Should().Be(new Fraction(expectedNum, expectedDen));
        result.Value.Should().BeGreaterThanOrEqualTo(1.0).And.BeLessThan(2.0);
    }

    [Fact]
    public void Renormalize_readme_example_major_chord_by_third_yields_minor_chord()
    {
        // README line 25: {1, 5/4, 3/2} renormalize with 5/4 as base → {1, 6/5, 8/5}.
        var family = new[] { new Fraction(1, 1), new Fraction(5, 4), new Fraction(3, 2) };

        var result = Renormalization.Renormalize(family, new Fraction(5, 4));

        result.Should().Equal(
            new Fraction(1, 1),
            new Fraction(6, 5),
            new Fraction(8, 5));
    }

    [Fact]
    public void Renormalize_lcm4_to_base_three_halves_yields_lcm3_family()
    {
        // README line 45: LCM4 {1, 5/4, 3/2} renormalize to 3/2 → LCM3 {1, 4/3, 5/3}.
        var family = new[] { new Fraction(1, 1), new Fraction(5, 4), new Fraction(3, 2) };

        var result = Renormalization.Renormalize(family, new Fraction(3, 2));

        result.Should().Equal(
            new Fraction(1, 1),
            new Fraction(4, 3),
            new Fraction(5, 3));
    }

    [Fact]
    public void Renormalize_lcm18_to_base_four_thirds_yields_subset_of_lcm24()
    {
        // README line 45: lcm 18 {1, 10/9, 4/3, 3/2, 5/3, 16/9} renormalize to 4/3
        // → {1, 9/8, 5/4, 4/3, 3/2, 5/3}, subset of lcm 24.
        var family = new[]
        {
            new Fraction(1, 1), new Fraction(10, 9), new Fraction(4, 3),
            new Fraction(3, 2), new Fraction(5, 3), new Fraction(16, 9),
        };

        var result = Renormalization.Renormalize(family, new Fraction(4, 3));

        result.Should().Equal(
            new Fraction(1, 1),
            new Fraction(9, 8),
            new Fraction(5, 4),
            new Fraction(4, 3),
            new Fraction(3, 2),
            new Fraction(5, 3));
    }

    [Fact]
    public void Renormalize_by_unity_is_octave_normalization_only()
    {
        var family = new[] { new Fraction(1, 1), new Fraction(5, 4), new Fraction(3, 2) };

        var result = Renormalization.Renormalize(family, new Fraction(1, 1));

        result.Should().Equal(family);
    }

    [Fact]
    public void Renormalize_result_is_sorted_ascending_by_value_and_in_octave()
    {
        var family = new[]
        {
            new Fraction(1, 1), new Fraction(9, 8), new Fraction(5, 4),
            new Fraction(4, 3), new Fraction(3, 2), new Fraction(5, 3),
            new Fraction(15, 8),
        };

        var result = Renormalization.Renormalize(family, new Fraction(5, 3));

        for (var i = 1; i < result.Count; i++)
        {
            result[i].Value.Should().BeGreaterThan(result[i - 1].Value);
        }
        foreach (var f in result)
        {
            f.Value.Should().BeGreaterThanOrEqualTo(1.0).And.BeLessThan(2.0);
        }
    }
}
