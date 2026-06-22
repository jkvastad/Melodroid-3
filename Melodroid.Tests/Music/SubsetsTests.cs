using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class SubsetsTests
{
    private const int Ktet = 12;

    [Fact]
    public void Enumerate_Lcm15_key_set_contains_a_full_match_for_Lcm20()
    {
        // The lcm-families graph reports LCM 20 as a renormalized subset of LCM 15.
        // At the keyboard level that means some subset of LCM 15's placement keys
        // full-matches the LCM-20 family. This pins that down.
        var (baseKeys, radius) = SetupForLcm(15);

        var (matches, _) = Subsets.Enumerate(baseKeys, GoodFractionsStd(), Ktet, radius, strictOnly: false, maxResults: 1000);

        matches.Should().Contain(m => m.Lcm == 20);
    }

    [Fact]
    public void Enumerate_major_triad_full_set_matches_Lcm4_at_reference_zero()
    {
        var baseKeys = new[] { 0, 4, 7 };
        var radius = RadiusFor(Ktet);

        var (matches, _) = Subsets.Enumerate(baseKeys, GoodFractionsStd(), Ktet, radius, strictOnly: false, maxResults: 1000);

        matches.Should().Contain(m =>
            m.Keys.SequenceEqual(new[] { 0, 4, 7 }) && m.Reference == 0 && m.Lcm == 4 && m.Strict);
        matches.Should().Contain(m => m.Keys.Count == 2);
    }

    [Fact]
    public void Enumerate_strict_match_exposes_a_single_candidate_lcm()
    {
        var baseKeys = new[] { 0, 4, 7 };
        var radius = RadiusFor(Ktet);

        var (matches, _) = Subsets.Enumerate(baseKeys, GoodFractionsStd(), Ktet, radius, strictOnly: false, maxResults: 1000);

        var strict = matches.First(m =>
            m.Keys.SequenceEqual(new[] { 0, 4, 7 }) && m.Reference == 0 && m.Strict);
        strict.Lcms.Should().ContainSingle().Which.Should().Be(4);
        strict.Lcm.Should().Be(strict.Lcms[0]);
    }

    [Fact]
    public void Enumerate_ambiguous_match_lists_candidate_lcms_ascending_best_fit_first()
    {
        var (baseKeys, radius) = SetupForLcm(15);

        var (matches, _) = Subsets.Enumerate(baseKeys, GoodFractionsStd(), Ktet, radius, strictOnly: false, maxResults: 1000);

        var ambiguous = matches.Where(m => !m.Strict).ToList();
        ambiguous.Should().NotBeEmpty();
        ambiguous.Should().OnlyContain(m => m.Lcms.Count >= 1);
        // Candidates are sorted ascending, with the best fit (PostBinLcm equivalent) first.
        ambiguous.Should().OnlyContain(m => m.Lcms.SequenceEqual(m.Lcms.OrderBy(x => x)));
        ambiguous.Should().OnlyContain(m => m.Lcm == m.Lcms.Min());
        // At least one ambiguous cell offers a genuine next-best alternative.
        ambiguous.Should().Contain(m => m.Lcms.Count >= 2);
    }

    [Fact]
    public void Enumerate_never_returns_subsets_smaller_than_two_keys()
    {
        var (baseKeys, radius) = SetupForLcm(15);

        var (matches, _) = Subsets.Enumerate(baseKeys, GoodFractionsStd(), Ktet, radius, strictOnly: false, maxResults: 1000);

        matches.Should().OnlyContain(m => m.Keys.Count >= 2);
    }

    [Fact]
    public void Enumerate_strictOnly_returns_only_strict_matches()
    {
        var (baseKeys, radius) = SetupForLcm(15);

        var (matches, _) = Subsets.Enumerate(baseKeys, GoodFractionsStd(), Ktet, radius, strictOnly: true, maxResults: 1000);

        matches.Should().OnlyContain(m => m.Strict);
    }

    [Fact]
    public void Enumerate_truncates_to_subset_rows_and_flags_when_exceeding_max()
    {
        var (baseKeys, radius) = SetupForLcm(15);

        var (full, fullTruncated) = Subsets.Enumerate(baseKeys, GoodFractionsStd(), Ktet, radius, strictOnly: false, maxResults: 1000);
        fullTruncated.Should().BeFalse();
        DistinctSubsets(full).Should().BeGreaterThan(3);

        // maxResults caps the number of distinct subsets (rows), not individual matches.
        var (capped, cappedTruncated) = Subsets.Enumerate(baseKeys, GoodFractionsStd(), Ktet, radius, strictOnly: false, maxResults: 3);
        cappedTruncated.Should().BeTrue();
        DistinctSubsets(capped).Should().Be(3);
    }

    private static int DistinctSubsets(IReadOnlyList<SubsetMatch> matches) =>
        matches.Select(m => string.Join(",", m.Keys)).Distinct().Count();

    private static IReadOnlyList<Fraction> GoodFractionsStd() =>
        GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);

    private static double RadiusFor(int ktet) =>
        KeysNeeded.WorstCaseForK(GoodFractionsStd(), ktet).Radius;

    private static (IReadOnlyList<int> BaseKeys, double Radius) SetupForLcm(int lcm)
    {
        var fractions = GoodFractionsStd();
        var families = LcmFamilies.Compute(fractions, maxLcm: 24);
        var family = families.Single(f => f.Lcm == lcm);
        var placement = Placements.Compute(family, at: 0, ktet: Ktet);
        var baseKeys = placement.Keys.Distinct().OrderBy(x => x).ToList();
        return (baseKeys, RadiusFor(Ktet));
    }
}
