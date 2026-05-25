using FluentAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class KeysNeededTests
{
    private static readonly IReadOnlyList<Fraction> DefaultGoodFractions =
        GoodFractions.Enumerate(24, 5);

    [Fact]
    public void Singleton_one_over_one_is_covered_by_k_equal_one()
    {
        var rows = KeysNeeded.Compute(
            new[] { new Fraction(1, 1) },
            startBinRadius: 1.0 / 161.0,
            maxBinRadius: 0.01,
            radiusStep: 0.005,
            maxK: 1000);

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(r => r.MinK == 1);
    }

    [Fact]
    public void Empty_fraction_set_finds_k_equal_one()
    {
        var rows = KeysNeeded.Compute(
            Array.Empty<Fraction>(),
            startBinRadius: 0.001,
            maxBinRadius: 0.01,
            radiusStep: 0.005,
            maxK: 1000);

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(r => r.MinK == 1);
    }

    [Fact]
    public void Max_k_one_with_multiple_fractions_at_tight_radius_yields_no_fit()
    {
        var rows = KeysNeeded.Compute(
            new[] { new Fraction(1, 1), new Fraction(3, 2) },
            startBinRadius: 0.001,
            maxBinRadius: 0.001,
            radiusStep: 0.001,
            maxK: 1);

        rows.Should().HaveCount(1);
        rows[0].MinK.Should().BeNull();
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.005)]
    [InlineData(0.001)]
    public void Two_fraction_set_min_k_matches_brute_force_oracle(double radius)
    {
        var fractions = new[] { new Fraction(1, 1), new Fraction(3, 2) };
        var rows = KeysNeeded.Compute(
            fractions,
            startBinRadius: radius,
            maxBinRadius: radius,
            radiusStep: radius,
            maxK: 200);

        rows.Should().HaveCount(1);
        rows[0].MinK.Should().Be(FindMinKOracle(fractions, radius, maxK: 200));
    }

    [Fact]
    public void Default_fractions_at_unambiguous_radius_match_brute_force_oracle()
    {
        var radius = 1.0 / 161.0;
        var rows = KeysNeeded.Compute(
            DefaultGoodFractions,
            startBinRadius: radius,
            maxBinRadius: radius,
            radiusStep: 0.001,
            maxK: 1000);

        rows.Should().HaveCount(1);
        var oracle = FindMinKOracle(DefaultGoodFractions, radius, maxK: 1000);
        rows[0].MinK.Should().Be(oracle);
        oracle.Should().NotBeNull("a finite k must exist since log2(g) is irrational for non-power-of-2 g");
    }

    [Fact]
    public void Sweep_min_k_is_non_increasing_as_radius_grows()
    {
        var rows = KeysNeeded.Compute(
            DefaultGoodFractions,
            startBinRadius: 1.0 / 161.0,
            maxBinRadius: 0.05,
            radiusStep: 0.001,
            maxK: 1000);

        rows.Should().NotBeEmpty();
        // Looser radius cannot make coverage harder, so MinK is monotonically non-increasing.
        // null (no-fit) is treated as +∞: it must only appear before fitted rows.
        var previous = int.MaxValue;
        foreach (var row in rows)
        {
            var current = row.MinK ?? int.MaxValue;
            current.Should().BeLessThanOrEqualTo(previous);
            previous = current;
        }
    }

    [Fact]
    public void Sweep_row_count_matches_step_count()
    {
        // start=0.005, max=0.020, step=0.005 → values {0.005, 0.010, 0.015, 0.020} = 4 rows.
        var rows = KeysNeeded.Compute(
            new[] { new Fraction(1, 1) },
            startBinRadius: 0.005,
            maxBinRadius: 0.020,
            radiusStep: 0.005,
            maxK: 10);

        rows.Should().HaveCount(4);
        rows[0].BinRadius.Should().BeApproximately(0.005, 1e-12);
        rows[^1].BinRadius.Should().BeApproximately(0.020, 1e-12);
    }

    [Fact]
    public void Max_below_start_returns_empty()
    {
        var rows = KeysNeeded.Compute(
            DefaultGoodFractions,
            startBinRadius: 0.02,
            maxBinRadius: 0.01,
            radiusStep: 0.001,
            maxK: 100);

        rows.Should().BeEmpty();
    }

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

    [Fact]
    public void Fraction_near_octave_wrap_is_covered_via_wrap_candidate()
    {
        // 47/24 ≈ 1.9583 — closest k-tet key is near n=k (which wraps to n=0).
        // At k=53, n=51 gives 2^(51/53) ≈ 1.948 → -0.53% from 47/24, within 1%.
        // This exercises the floor/ceil candidate-narrowing path including the wrap.
        var fractions = new[] { new Fraction(1, 1), new Fraction(47, 24) };
        var rows = KeysNeeded.Compute(
            fractions,
            startBinRadius: 0.01,
            maxBinRadius: 0.01,
            radiusStep: 0.01,
            maxK: 200);

        rows.Should().HaveCount(1);
        rows[0].MinK.Should().Be(FindMinKOracle(fractions, 0.01, maxK: 200));
        rows[0].MinK.Should().NotBeNull();
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
        // c_k is the exact threshold: at radius = c_k coverage is achieved (≤),
        // and infinitesimally below it must fail.
        KeysNeeded.IsKtetCoverage(DefaultGoodFractions, k, row.Radius).Should().BeTrue();
        KeysNeeded.IsKtetCoverage(DefaultGoodFractions, k, row.Radius - 1e-9).Should().BeFalse();
    }

    [Fact]
    public void ComputeCutoffs_min_k_matches_ktet_min_keys_sweep()
    {
        // For any radius r, Min_k(r) from KeysNeeded.Compute should equal
        // min { k : c_k <= r } derived from ComputeCutoffs.
        const int maxK = 60;
        var cutoffs = KeysNeeded.ComputeCutoffs(DefaultGoodFractions, maxK);

        var sweep = KeysNeeded.Compute(
            DefaultGoodFractions,
            startBinRadius: 1.0 / 161.0,
            maxBinRadius: 0.02,
            radiusStep: 0.0005,
            maxK: maxK);

        foreach (var row in sweep)
        {
            int? expected = null;
            foreach (var c in cutoffs)
            {
                if (c.Radius <= row.BinRadius) { expected = c.K; break; }
            }
            row.MinK.Should().Be(expected,
                because: $"radius {row.BinRadius}: cutoffs and sweep must agree");
        }
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

    // Independent brute-force oracle: enumerate every n ∈ [0, k) and test the
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

    private static int? FindMinKOracle(IReadOnlyList<Fraction> fractions, double radius, int maxK)
    {
        for (var k = 1; k <= maxK; k++)
        {
            if (IsCoveredOracle(fractions, k, radius)) return k;
        }
        return null;
    }
}
