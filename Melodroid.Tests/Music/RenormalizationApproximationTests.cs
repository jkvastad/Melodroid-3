using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class RenormalizationApproximationTests
{
    // Standard good-fraction set (max size 24, max prime 5).
    private static readonly IReadOnlyList<Fraction> GoodFractions =
        Melodroid_3.Music.GoodFractions.Enumerate(24, 5);

    private static readonly Fraction[] Lcm18 =
    {
        new(1, 1), new(10, 9), new(4, 3),
        new(3, 2), new(5, 3), new(16, 9),
    };

    [Fact]
    public void Snap_lcm18_by_sixteen_ninths_needs_syntonic_comma_and_lands_in_lcm24()
    {
        // 16/9 renormalises lcm 18 into the lcm-24 major scale once 27/16 snaps to 5/3.
        var images = Renormalization.Renormalize(Lcm18, new Fraction(16, 9));

        var snapped = RenormalizationApproximation.Snap(images, GoodFractions);

        // The single non-good image is 27/16, snapping to 5/3 at exactly the syntonic comma.
        var bad = snapped.Where(s => !s.AlreadyGood).ToList();
        bad.Should().ContainSingle();
        bad[0].Image.Should().Be(new Fraction(27, 16));
        bad[0].Nearest.Should().Be(new Fraction(5, 3));
        bad[0].Radius.Should().Be(new Fraction(1, 161));

        // Every image snaps onto a good fraction, giving an all-good renormalisation.
        var goodSet = new HashSet<Fraction>(GoodFractions);
        snapped.Should().OnlyContain(s => goodSet.Contains(s.Nearest));

        RenormalizationApproximation.RequiredRadius(snapped).Should().Be(new Fraction(1, 161));
    }

    [Fact]
    public void Snap_already_good_renormalization_requires_zero_radius()
    {
        // lcm 18 by 4/3 is an exact subset of lcm 24 — no snapping needed.
        var images = Renormalization.Renormalize(Lcm18, new Fraction(4, 3));

        var snapped = RenormalizationApproximation.Snap(images, GoodFractions);

        snapped.Should().OnlyContain(s => s.AlreadyGood);
        snapped.Should().OnlyContain(s => s.Radius == new Fraction(0, 1));
        snapped.Should().OnlyContain(s => s.Nearest == s.Image);
        RenormalizationApproximation.RequiredRadius(snapped).Should().Be(new Fraction(0, 1));
    }

    [Fact]
    public void RequiredRadius_is_the_largest_snap_over_the_images()
    {
        // lcm 24 by 4/3 snaps 45/32 to 4/3 at 7/263 — larger than any other image's snap.
        var lcm24 = new[]
        {
            new Fraction(1, 1), new Fraction(9, 8), new Fraction(5, 4),
            new Fraction(4, 3), new Fraction(3, 2), new Fraction(5, 3),
            new Fraction(15, 8),
        };
        var images = Renormalization.Renormalize(lcm24, new Fraction(4, 3));

        var snapped = RenormalizationApproximation.Snap(images, GoodFractions);

        RenormalizationApproximation.RequiredRadius(snapped).Should().Be(new Fraction(7, 263));
    }

    [Fact]
    public void NearestGoodFraction_snaps_bad_fraction_to_closest_good_one()
    {
        var nearest = RenormalizationApproximation.NearestGoodFraction(
            new Fraction(128, 81), GoodFractions, out var distance);

        nearest.Should().Be(new Fraction(8, 5));
        distance.Should().BeApproximately(1.0 / 161, 1e-9);
    }
}
