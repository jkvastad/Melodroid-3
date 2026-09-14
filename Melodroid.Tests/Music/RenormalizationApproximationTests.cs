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

    // lcm-24 major scale, renormalised onto 5/3 — the row where per-fraction nearest snapping
    // (27/20 → 4/3) balloons the wave-pattern length to 120 while staying near the JND.
    private static readonly Fraction[] Lcm24 =
    {
        new(1, 1), new(9, 8), new(5, 4),
        new(4, 3), new(3, 2), new(5, 3),
        new(15, 8),
    };

    [Fact]
    public void SnapFrontier_offers_a_low_lcm_alternative_to_a_ballooning_nearest_snap()
    {
        var images = Renormalization.Renormalize(Lcm24, new Fraction(5, 3));

        // Nearest per-fraction snapping alone lands on an lcm-120 set — beyond the ~24 ceiling.
        var nearest = RenormalizationApproximation.Snap(images, GoodFractions);
        RatioMath.WavePatternLength(nearest.Select(s => s.Nearest)).Should().Be(120);

        // Trading a worse snap buys an lcm ≤ 24 option: the frontier holds it down to 24.
        var frontier = RenormalizationApproximation.SnapFrontier(images, GoodFractions, maxLcm: 24, maxC: 0.05);
        frontier.Should().Contain(p => p.Lcm == 24);
        frontier.Max(p => p.Lcm).Should().BeLessThanOrEqualTo(24);

        // The lcm-24 option costs more radius than nearest snapping but stays well within maxC.
        var at24 = frontier.Single(p => p.Lcm == 24);
        at24.WorstRadius.Value.Should().BeGreaterThan(1.0 / 161).And.BeLessThan(0.05);
    }

    [Fact]
    public void SnapFrontier_trades_radius_for_lcm_monotonically()
    {
        var images = Renormalization.Renormalize(Lcm24, new Fraction(5, 3));

        var frontier = RenormalizationApproximation.SnapFrontier(images, GoodFractions, maxLcm: 24, maxC: 0.05);

        // A genuine tradeoff: several points, radius strictly falling as the LCM rises.
        frontier.Count.Should().BeGreaterThan(1);
        for (var i = 1; i < frontier.Count; i++)
        {
            frontier[i].Lcm.Should().BeGreaterThan(frontier[i - 1].Lcm);
            frontier[i].WorstRadius.Value.Should().BeLessThan(frontier[i - 1].WorstRadius.Value);
        }
    }

    [Fact]
    public void SnapFrontier_points_are_all_good_within_bound_and_match_their_reported_lcm()
    {
        var images = Renormalization.Renormalize(Lcm24, new Fraction(5, 3));
        var goodSet = new HashSet<Fraction>(GoodFractions);

        var frontier = RenormalizationApproximation.SnapFrontier(images, GoodFractions, maxLcm: 24, maxC: 0.05);

        frontier.Should().OnlyContain(p => p.Snapped.All(f => goodSet.Contains(f)));
        frontier.Should().OnlyContain(p => p.Lcm <= 24);
        frontier.Should().OnlyContain(p => p.WorstRadius.Value <= 0.05);
        frontier.Should().OnlyContain(p => RatioMath.WavePatternLength(p.Snapped) == p.Lcm);
    }

    [Fact]
    public void SnapFrontier_tightening_the_bound_only_drops_options()
    {
        var images = Renormalization.Renormalize(Lcm24, new Fraction(5, 3));

        var loose = RenormalizationApproximation.SnapFrontier(images, GoodFractions, maxLcm: 24, maxC: 0.05);
        // 0.5 % is below the ~0.62 % that 27/20 needs to reach any good fraction, so nothing fits.
        var tight = RenormalizationApproximation.SnapFrontier(images, GoodFractions, maxLcm: 24, maxC: 0.005);

        loose.Should().NotBeEmpty();
        tight.Should().BeEmpty();
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
