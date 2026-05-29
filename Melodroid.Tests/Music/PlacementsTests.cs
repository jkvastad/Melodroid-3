using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class PlacementsTests
{
    [Fact]
    public void Compute_Lcm4_at_zero_on_12tet_is_C_major_triad()
    {
        var family = FamilyFor(lcm: 4);

        var placement = Placements.Compute(family, at: 0, ktet: 12);

        placement.Lcm.Should().Be(4);
        placement.At.Should().Be(0);
        placement.Keys.Should().Equal(0, 4, 7);
    }

    [Fact]
    public void Compute_Lcm4_at_seven_on_12tet_is_G_major_triad()
    {
        var family = FamilyFor(lcm: 4);

        var placement = Placements.Compute(family, at: 7, ktet: 12);

        placement.Lcm.Should().Be(4);
        placement.At.Should().Be(7);
        placement.Keys.Should().Equal(7, 11, 2);
    }

    [Fact]
    public void Compute_Lcm24_at_zero_on_12tet_is_C_major_scale()
    {
        var family = FamilyFor(lcm: 24);

        var placement = Placements.Compute(family, at: 0, ktet: 12);

        placement.Keys.Should().Equal(0, 2, 4, 5, 7, 9, 11);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(19)]
    [InlineData(31)]
    public void Sweep_returns_one_placement_per_key(int ktet)
    {
        var family = FamilyFor(lcm: 4);

        var sweep = Placements.Sweep(family, ktet);

        sweep.Should().HaveCount(ktet);
        for (var at = 0; at < ktet; at++)
        {
            sweep[at].At.Should().Be(at);
            sweep[at].Keys.Should().Equal(Placements.Compute(family, at, ktet).Keys);
        }
    }

    [Fact]
    public void OverlapSweep_Lcm4_against_Lcm24_at_zero_intersects_with_triad()
    {
        var a = FamilyFor(lcm: 4);
        var b = FamilyFor(lcm: 24);

        var (bKeys, rows) = Placements.OverlapSweep(a, b, ktet: 12);

        bKeys.Should().Equal(0, 2, 4, 5, 7, 9, 11);
        rows.Should().HaveCount(12);

        var row0 = rows.Single(r => r.At == 0);
        row0.Intersection.Should().Equal(0, 4, 7);

        // F major triad (LCM 4 @ 5) keys are {5, 9, 0}; intersected with C major scale yields {0, 5, 9}.
        var row5 = rows.Single(r => r.At == 5);
        row5.AKeys.Should().Equal(5, 9, 0);
        row5.Intersection.Should().Equal(0, 5, 9);
    }

    [Fact]
    public void FindSupersets_triad_lists_every_known_C_major_triad_placement()
    {
        var families = LcmFamilies.Compute(
            GoodFractions.Enumerate(maxSize: 24, maxPrime: 5),
            maxLcm: 24);

        var rows = Placements.FindSupersets(new[] { 0, 4, 7 }, families, ktet: 12);

        // The tightest fit is extra=0. Two isomorphic placements achieve it:
        // (LCM 4, @0) = {0, 4, 7} directly, and (LCM 3, @7) = {0, 5, 9}+7 = {7, 0, 4}.
        rows[0].ExtraKeysCount.Should().Be(0);
        rows.Where(r => r.ExtraKeysCount == 0)
            .Select(r => (r.Placement.Lcm, r.Placement.At))
            .Should().BeEquivalentTo(new[] { (3, 7), (4, 0) });

        // (LCM 8, at 0), (LCM 12, at 0), (LCM 24, at 0) must all appear as supersets.
        rows.Should().Contain(r => r.Placement.Lcm == 8 && r.Placement.At == 0);
        rows.Should().Contain(r => r.Placement.Lcm == 12 && r.Placement.At == 0);
        rows.Should().Contain(r => r.Placement.Lcm == 24 && r.Placement.At == 0);
    }

    [Fact]
    public void FindSupersets_rows_are_sorted_by_extra_then_lcm_then_at()
    {
        var families = LcmFamilies.Compute(
            GoodFractions.Enumerate(maxSize: 24, maxPrime: 5),
            maxLcm: 24);

        var rows = Placements.FindSupersets(new[] { 0, 4, 7 }, families, ktet: 12);

        static (int, int, int) KeyOf(KeySupersetRow r) => (r.ExtraKeysCount, r.Placement.Lcm, r.Placement.At);
        for (var i = 1; i < rows.Count; i++)
        {
            var prev = KeyOf(rows[i - 1]);
            var cur = KeyOf(rows[i]);
            Comparer<(int, int, int)>.Default.Compare(cur, prev).Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public void FindMaximalContaining_triad_returns_15_at_4_7_11_and_24_at_0_5_7()
    {
        var families = LcmFamilies.Compute(
            GoodFractions.Enumerate(maxSize: 24, maxPrime: 5),
            maxLcm: 24);
        var relations = FamilyRelations.Compute(families);

        var placements = Placements.FindMaximalContaining(new[] { 0, 4, 7 }, families, relations, ktet: 12);

        placements
            .Select(p => (p.Lcm, p.At))
            .Should().Equal(new[] { (15, 4), (15, 7), (15, 11), (24, 0), (24, 5), (24, 7) });
    }

    [Fact]
    public void FindMaximalContaining_full_C_major_scale_returns_only_24_at_0()
    {
        var families = LcmFamilies.Compute(
            GoodFractions.Enumerate(maxSize: 24, maxPrime: 5),
            maxLcm: 24);
        var relations = FamilyRelations.Compute(families);

        var placements = Placements.FindMaximalContaining(
            new[] { 0, 2, 4, 5, 7, 9, 11 }, families, relations, ktet: 12);

        placements.Should().HaveCount(1);
        placements[0].Lcm.Should().Be(24);
        placements[0].At.Should().Be(0);
    }

    [Fact]
    public void FindMaximalContaining_dim7_returns_empty()
    {
        var families = LcmFamilies.Compute(
            GoodFractions.Enumerate(maxSize: 24, maxPrime: 5),
            maxLcm: 24);
        var relations = FamilyRelations.Compute(families);

        // Dim7 {0, 3, 6, 9} is the symmetric 12/4 cycle and per the README is
        // not present in any LCM family — must yield zero maximal placements.
        var placements = Placements.FindMaximalContaining(new[] { 0, 3, 6, 9 }, families, relations, ktet: 12);

        placements.Should().BeEmpty();
    }

    [Fact]
    public void FindMaximalContaining_rows_sorted_by_lcm_then_at()
    {
        var families = LcmFamilies.Compute(
            GoodFractions.Enumerate(maxSize: 24, maxPrime: 5),
            maxLcm: 24);
        var relations = FamilyRelations.Compute(families);

        var placements = Placements.FindMaximalContaining(new[] { 0, 4, 7 }, families, relations, ktet: 12);

        for (var i = 1; i < placements.Count; i++)
        {
            var prev = (placements[i - 1].Lcm, placements[i - 1].At);
            var cur = (placements[i].Lcm, placements[i].At);
            Comparer<(int, int)>.Default.Compare(cur, prev).Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public void MaximalLcms_with_default_params_yields_15_and_24()
    {
        var families = LcmFamilies.Compute(
            GoodFractions.Enumerate(maxSize: 24, maxPrime: 5),
            maxLcm: 24);
        var relations = FamilyRelations.Compute(families);

        // Independent oracle: an LCM is maximal iff no other family contains it (as literal
        // subset) and no other family contains its renormalization (as renormalized subset).
        var familyByLcm = families.ToDictionary(f => f.Lcm);
        var unity = new Fraction(1, 1);
        bool IsDominated(int lcm)
        {
            var a = familyByLcm[lcm];
            foreach (var b in families)
            {
                if (b.Lcm == lcm) continue;
                if (a.Fractions.Count < b.Fractions.Count)
                {
                    var bSet = new HashSet<Fraction>(b.Fractions);
                    if (a.Fractions.All(bSet.Contains)) return true;
                    foreach (var baseFrac in a.Fractions)
                    {
                        if (baseFrac == unity) continue;
                        var ren = Renormalization.Renormalize(a.Fractions, baseFrac);
                        if (ren.All(bSet.Contains)) return true;
                    }
                }
            }
            return false;
        }
        var expected = families
            .Select(f => f.Lcm)
            .Where(lcm => !IsDominated(lcm))
            .OrderBy(lcm => lcm)
            .ToList();

        var actual = Placements.MaximalLcms(families, relations);

        actual.Should().Equal(expected);
        expected.Should().Equal(new[] { 15, 24 });
    }

    private static LcmFamily FamilyFor(int lcm)
    {
        var goodFractions = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);
        var families = LcmFamilies.Compute(goodFractions, maxLcm: 24);
        return families.Single(f => f.Lcm == lcm);
    }
}
