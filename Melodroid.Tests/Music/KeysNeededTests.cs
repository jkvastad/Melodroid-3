using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class KeysNeededTests
{
    private static readonly IReadOnlyList<Fraction> DefaultGoodFractions =
        GoodFractions.Enumerate(24, 5);

    [Fact]
    public void IsKtetCoverage_at_reported_min_k_matches_oracle()
    {
        // Spot-check that the public predicate agrees with the brute-force oracle
        // across a range of k for a fixed radius.
        var radius = 0.01;
        for (var k = 1; k <= 30; k++)
        {
            KeysNeeded.IsKtetCoverage(DefaultGoodFractions, k, radius)
                .Should().Be(IsCoveredOracle(DefaultGoodFractions, k, radius),
                    because: $"k={k} should agree with oracle");
        }
    }

    [Theory]
    [InlineData(1.5, 12)]
    [InlineData(1.25, 12)]
    [InlineData(5.0 / 3.0, 19)]
    [InlineData(15.0 / 8.0, 31)]
    [InlineData(47.0 / 24.0, 53)]
    [InlineData(1.0, 7)]
    public void NearestKey_signed_relative_matches_min_over_all_n_oracle(double g, int k)
    {
        var nk = KeysNeeded.NearestKey(g, k);

        var oracle = NearestKeyOracle(g, k);
        nk.N.Should().Be(oracle.N);
        nk.KeyRatio.Should().BeApproximately(oracle.KeyRatio, 1e-12);
        Math.Abs(nk.SignedRelative).Should()
            .BeApproximately(Math.Abs(oracle.SignedRelative), 1e-12);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(19)]
    [InlineData(31)]
    [InlineData(34)]
    public void WorstCaseForK_radius_equals_max_over_g_of_nearest_distance(int k)
    {
        var row = KeysNeeded.WorstCaseForK(DefaultGoodFractions, k);

        var (oracleRadius, oracleFraction) = WorstCaseOracle(DefaultGoodFractions, k);
        row.Radius.Should().BeApproximately(oracleRadius, 1e-12);
        row.LimitingFraction.Should().Be(oracleFraction);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(19)]
    [InlineData(31)]
    public void WorstCaseForK_radius_is_tight_against_IsKtetCoverage(int k)
    {
        var row = KeysNeeded.WorstCaseForK(DefaultGoodFractions, k);
        // c_k is the exact threshold: at radius = c_k coverage is achieved (â‰¤),
        // and infinitesimally below it must fail.
        KeysNeeded.IsKtetCoverage(DefaultGoodFractions, k, row.Radius).Should().BeTrue();
        KeysNeeded.IsKtetCoverage(DefaultGoodFractions, k, row.Radius - 1e-9).Should().BeFalse();
    }

    private static KtetNearestKey NearestKeyOracle(double g, int k)
    {
        var bestN = 0;
        var bestRel = double.PositiveInfinity;
        var bestRatio = 1.0;
        for (var n = 0; n < k; n++)
        {
            var keyRatio = Math.Pow(2.0, (double)n / k);
            var direct = (keyRatio - g) / g;
            var wrapUp = (2.0 * keyRatio - g) / g;
            var wrapDn = (keyRatio - 2.0 * g) / g;
            var best = direct;
            if (Math.Abs(wrapUp) < Math.Abs(best)) best = wrapUp;
            if (Math.Abs(wrapDn) < Math.Abs(best)) best = wrapDn;
            if (Math.Abs(best) < Math.Abs(bestRel))
            {
                bestRel = best;
                bestN = n;
                bestRatio = keyRatio;
            }
        }
        return new KtetNearestKey(bestN, bestRatio, bestRel);
    }

    private static (double Radius, Fraction LimitingFraction) WorstCaseOracle(
        IReadOnlyList<Fraction> fractions, int k)
    {
        var worst = -1.0;
        var worstFraction = fractions[0];
        foreach (var g in fractions)
        {
            var nk = NearestKeyOracle(g.Value, k);
            var dist = Math.Abs(nk.SignedRelative);
            if (dist > worst)
            {
                worst = dist;
                worstFraction = g;
            }
        }
        return (worst, worstFraction);
    }

    // Independent brute-force oracle: enumerate every n âˆˆ [0, k) and test the
    // multiplicative circular distance to each good fraction inline. Reimplements
    // the binning predicate from scratch so the production code is checked against
    // an independent route, not a refactor of itself.
    private static bool IsCoveredOracle(IReadOnlyList<Fraction> fractions, int k, double radius)
    {
        foreach (var g in fractions)
        {
            var covered = false;
            for (var n = 0; n < k; n++)
            {
                var keyRatio = Math.Pow(2.0, (double)n / k);
                var direct = (keyRatio - g.Value) / g.Value;
                var wrapUp = (2.0 * keyRatio - g.Value) / g.Value;
                var wrapDn = (keyRatio - 2.0 * g.Value) / g.Value;
                var best = direct;
                if (Math.Abs(wrapUp) < Math.Abs(best)) best = wrapUp;
                if (Math.Abs(wrapDn) < Math.Abs(best)) best = wrapDn;
                if (Math.Abs(best) <= radius) { covered = true; break; }
            }
            if (!covered) return false;
        }
        return true;
    }

}

