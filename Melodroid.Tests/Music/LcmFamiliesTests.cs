using FluentAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class LcmFamiliesTests
{
    [Fact]
    public void Compute_defaults_returns_hand_verified_families_ordered_by_lcm()
    {
        var goodFractions = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);

        var families = LcmFamilies.Compute(goodFractions, maxLcm: 24);

        families.Select(fm => fm.Lcm).Should().Equal(
            1, 2, 3, 4, 5, 6, 8, 9, 10, 12, 15, 18, 20, 24);

        FamilyFor(families, 1).Should().Equal(new Fraction(1, 1));
        FamilyFor(families, 2).Should().Equal(new Fraction(1, 1), new Fraction(3, 2));
        FamilyFor(families, 3).Should().Equal(new Fraction(1, 1), new Fraction(4, 3), new Fraction(5, 3));
        FamilyFor(families, 4).Should().Equal(new Fraction(1, 1), new Fraction(5, 4), new Fraction(3, 2));
        FamilyFor(families, 5).Should().Equal(
            new Fraction(1, 1), new Fraction(6, 5), new Fraction(8, 5), new Fraction(9, 5));
        FamilyFor(families, 6).Should().Equal(
            new Fraction(1, 1), new Fraction(4, 3), new Fraction(3, 2), new Fraction(5, 3));
        FamilyFor(families, 8).Should().Equal(
            new Fraction(1, 1), new Fraction(9, 8), new Fraction(5, 4), new Fraction(3, 2), new Fraction(15, 8));
        FamilyFor(families, 9).Should().Equal(
            new Fraction(1, 1), new Fraction(10, 9), new Fraction(4, 3), new Fraction(5, 3), new Fraction(16, 9));
        FamilyFor(families, 10).Should().Equal(
            new Fraction(1, 1), new Fraction(6, 5), new Fraction(3, 2),
            new Fraction(8, 5), new Fraction(9, 5));
        FamilyFor(families, 12).Should().Equal(
            new Fraction(1, 1), new Fraction(5, 4), new Fraction(4, 3),
            new Fraction(3, 2), new Fraction(5, 3));
        FamilyFor(families, 15).Should().Equal(
            new Fraction(1, 1), new Fraction(16, 15), new Fraction(6, 5),
            new Fraction(4, 3), new Fraction(8, 5), new Fraction(5, 3), new Fraction(9, 5));
        FamilyFor(families, 18).Should().Equal(
            new Fraction(1, 1), new Fraction(10, 9), new Fraction(4, 3),
            new Fraction(3, 2), new Fraction(5, 3), new Fraction(16, 9));
        FamilyFor(families, 20).Should().Equal(
            new Fraction(1, 1), new Fraction(6, 5), new Fraction(5, 4),
            new Fraction(3, 2), new Fraction(8, 5), new Fraction(9, 5));
        // L=24 is the major scale played as a chord (Ionian: C D E F G A B).
        FamilyFor(families, 24).Should().Equal(
            new Fraction(1, 1), new Fraction(9, 8), new Fraction(5, 4),
            new Fraction(4, 3), new Fraction(3, 2), new Fraction(5, 3),
            new Fraction(15, 8));
    }

    [Fact]
    public void Empty_families_are_omitted_under_defaults()
    {
        var goodFractions = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);

        var families = LcmFamilies.Compute(goodFractions, maxLcm: 24);

        var lcms = families.Select(fm => fm.Lcm).ToHashSet();
        foreach (var emptyL in new[] { 7, 11, 13, 14, 16, 17, 19, 21, 22, 23 })
        {
            lcms.Should().NotContain(emptyL);
        }
    }

    [Theory]
    [InlineData(24, 5, 24)]
    [InlineData(24, 5, 12)]
    [InlineData(24, 3, 24)]
    [InlineData(15, 5, 30)]
    [InlineData(4, 3, 24)]
    public void Every_family_has_lcm_of_denominators_equal_to_its_lcm(int maxSize, int maxPrime, int maxLcm)
    {
        var goodFractions = GoodFractions.Enumerate(maxSize, maxPrime);

        var families = LcmFamilies.Compute(goodFractions, maxLcm);

        foreach (var family in families)
        {
            family.Fractions.Should().NotBeEmpty();
            var folded = family.Fractions.Aggregate(1, (acc, f) => Lcm(acc, f.Denominator));
            folded.Should().Be(family.Lcm);
            family.Lcm.Should().BeLessThanOrEqualTo(maxLcm);
        }
    }

    [Theory]
    [InlineData(24, 5, 24)]
    [InlineData(15, 5, 30)]
    [InlineData(24, 3, 24)]
    public void Result_is_strictly_ascending_by_lcm(int maxSize, int maxPrime, int maxLcm)
    {
        var goodFractions = GoodFractions.Enumerate(maxSize, maxPrime);

        var lcms = LcmFamilies.Compute(goodFractions, maxLcm).Select(fm => fm.Lcm).ToArray();

        for (var i = 1; i < lcms.Length; i++)
        {
            lcms[i].Should().BeGreaterThan(lcms[i - 1]);
        }
    }

    [Theory]
    [InlineData(24, 5, 24)]
    [InlineData(24, 5, 1)]
    public void Family_for_lcm_one_contains_only_unison_when_present(int maxSize, int maxPrime, int maxLcm)
    {
        var goodFractions = GoodFractions.Enumerate(maxSize, maxPrime);

        var families = LcmFamilies.Compute(goodFractions, maxLcm);

        FamilyFor(families, 1).Should().Equal(new Fraction(1, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_maxLcm_returns_empty_list_without_throwing(int maxLcm)
    {
        var goodFractions = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);

        var act = () => LcmFamilies.Compute(goodFractions, maxLcm);

        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Fact]
    public void Fractions_with_denominator_not_dividing_lcm_are_excluded()
    {
        // 16/15 has denominator 15; it should appear only in families whose LCM is a multiple of 15.
        var goodFractions = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);

        var families = LcmFamilies.Compute(goodFractions, maxLcm: 24);

        foreach (var family in families)
        {
            var contains16Over15 = family.Fractions.Contains(new Fraction(16, 15));
            if (contains16Over15)
            {
                (family.Lcm % 15).Should().Be(0);
            }
        }
    }

    private static IReadOnlyList<Fraction> FamilyFor(IReadOnlyList<LcmFamily> families, int lcm)
        => families.Single(fm => fm.Lcm == lcm).Fractions;

    private static int Lcm(int a, int b) => a / Gcd(a, b) * b;

    private static int Gcd(int a, int b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }
}
