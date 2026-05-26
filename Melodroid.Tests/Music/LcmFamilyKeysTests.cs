using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class LcmFamilyKeysTests
{
    [Fact]
    public void Lcm4_on_12tet_maps_to_major_triad_keys()
    {
        var rows = ComputeRowsFor(maxSize: 24, maxPrime: 5, maxLcm: 24, k: 12);

        RowFor(rows, lcm: 4).KeyIndices.Should().Equal(0, 4, 7);
    }

    [Fact]
    public void Lcm3_on_12tet_maps_to_fourth_and_sixth_keys()
    {
        var rows = ComputeRowsFor(maxSize: 24, maxPrime: 5, maxLcm: 24, k: 12);

        // {1/1, 4/3, 5/3} → keys 0, 5, 9 (C, F, A)
        RowFor(rows, lcm: 3).KeyIndices.Should().Equal(0, 5, 9);
    }

    [Fact]
    public void Lcm24_on_12tet_maps_to_major_scale_keys()
    {
        var rows = ComputeRowsFor(maxSize: 24, maxPrime: 5, maxLcm: 24, k: 12);

        // {1/1, 9/8, 5/4, 4/3, 3/2, 5/3, 15/8} → keys 0, 2, 4, 5, 7, 9, 11
        RowFor(rows, lcm: 24).KeyIndices.Should().Equal(0, 2, 4, 5, 7, 9, 11);
    }

    [Fact]
    public void Lcm1_maps_to_single_zero_key()
    {
        var rows = ComputeRowsFor(maxSize: 24, maxPrime: 5, maxLcm: 24, k: 12);

        RowFor(rows, lcm: 1).KeyIndices.Should().Equal(0);
    }

    [Fact]
    public void On_1tet_every_fraction_collapses_to_key_zero()
    {
        // 1-tet only has key 0 (ratio 1.0); every good fraction must map to it,
        // exercising the duplicate-keys-per-family path.
        var rows = ComputeRowsFor(maxSize: 24, maxPrime: 5, maxLcm: 24, k: 1);

        var lcm4 = RowFor(rows, lcm: 4);
        lcm4.Fractions.Should().HaveCount(3);
        lcm4.KeyIndices.Should().Equal(0, 0, 0);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(19)]
    [InlineData(31)]
    public void KeyIndices_count_always_matches_Fractions_count(int k)
    {
        var rows = ComputeRowsFor(maxSize: 24, maxPrime: 5, maxLcm: 24, k: k);

        foreach (var row in rows)
        {
            row.KeyIndices.Should().HaveCount(row.Fractions.Count);
        }
    }

    [Fact]
    public void Lcm_and_Fractions_are_preserved_from_input_families()
    {
        var goodFractions = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);
        var families = LcmFamilies.Compute(goodFractions, maxLcm: 24);

        var rows = LcmFamilyKeys.Compute(families, k: 12);

        rows.Should().HaveSameCount(families);
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].Lcm.Should().Be(families[i].Lcm);
            rows[i].Fractions.Should().Equal(families[i].Fractions);
        }
    }

    [Fact]
    public void Empty_families_list_returns_empty_rows()
    {
        var rows = LcmFamilyKeys.Compute(Array.Empty<LcmFamily>(), k: 12);

        rows.Should().BeEmpty();
    }

    [Fact]
    public void Collapsed_picks_lowest_lcm_per_class()
    {
        // LCM 3 {1/1, 4/3, 5/3} and LCM 4 {1/1, 5/4, 3/2} are isomorphic
        // (renormalize LCM 4 onto 3/2 → LCM 3). The collapsed row for that
        // class must pick LCM 3 as the representative — so the keys shown
        // are 4/3 → 5, 5/3 → 9 (not the C-major-triad 0 4 7).
        var rows = ComputeCollapsedRowsFor(maxSize: 24, maxPrime: 5, maxLcm: 24, k: 12);

        var classWith3and4 = rows.Single(r => r.ClassLcms.Contains(3) && r.ClassLcms.Contains(4));
        classWith3and4.RepresentativeLcm.Should().Be(3);
        classWith3and4.ClassLcms.Should().Equal(3, 4);
        classWith3and4.KeyIndices.Should().Equal(0, 5, 9);
    }

    [Fact]
    public void Collapsed_singletons_render_as_one_element_classes()
    {
        var rows = ComputeCollapsedRowsFor(maxSize: 24, maxPrime: 5, maxLcm: 24, k: 12);

        // LCM 1 (trivial) cannot be isomorphic to any other family (sizes differ).
        var singletonOne = rows.Single(r => r.RepresentativeLcm == 1);
        singletonOne.ClassLcms.Should().Equal(1);
    }

    [Fact]
    public void Collapsed_row_count_strictly_less_than_full_for_defaults()
    {
        var goodFractions = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);
        var families = LcmFamilies.Compute(goodFractions, maxLcm: 24);
        var relations = FamilyRelations.Compute(families);

        var full = LcmFamilyKeys.Compute(families, k: 12);
        var collapsed = LcmFamilyKeys.ComputeCollapsed(families, relations, k: 12);

        collapsed.Count.Should().BeLessThan(full.Count);
    }

    [Fact]
    public void Collapsed_rows_ordered_by_representative_lcm()
    {
        var rows = ComputeCollapsedRowsFor(maxSize: 24, maxPrime: 5, maxLcm: 24, k: 12);

        rows.Select(r => r.RepresentativeLcm).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Collapsed_class_lcms_start_with_representative_and_are_sorted()
    {
        var rows = ComputeCollapsedRowsFor(maxSize: 24, maxPrime: 5, maxLcm: 24, k: 12);

        foreach (var row in rows)
        {
            row.ClassLcms.Should().NotBeEmpty();
            row.ClassLcms[0].Should().Be(row.RepresentativeLcm);
            row.ClassLcms.Should().BeInAscendingOrder();
        }
    }

    [Fact]
    public void Collapsed_representative_fractions_match_lowest_lcm_family()
    {
        var goodFractions = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);
        var families = LcmFamilies.Compute(goodFractions, maxLcm: 24);
        var relations = FamilyRelations.Compute(families);
        var rows = LcmFamilyKeys.ComputeCollapsed(families, relations, k: 12);

        foreach (var row in rows)
        {
            var repFamily = families.Single(f => f.Lcm == row.RepresentativeLcm);
            row.Fractions.Should().Equal(repFamily.Fractions);
            row.KeyIndices.Should().HaveCount(repFamily.Fractions.Count);
        }
    }

    private static IReadOnlyList<LcmFamilyKeyRow> ComputeRowsFor(int maxSize, int maxPrime, int maxLcm, int k)
    {
        var goodFractions = GoodFractions.Enumerate(maxSize, maxPrime);
        var families = LcmFamilies.Compute(goodFractions, maxLcm);
        return LcmFamilyKeys.Compute(families, k);
    }

    private static IReadOnlyList<LcmFamilyKeyCollapsedRow> ComputeCollapsedRowsFor(int maxSize, int maxPrime, int maxLcm, int k)
    {
        var goodFractions = GoodFractions.Enumerate(maxSize, maxPrime);
        var families = LcmFamilies.Compute(goodFractions, maxLcm);
        var relations = FamilyRelations.Compute(families);
        return LcmFamilyKeys.ComputeCollapsed(families, relations, k);
    }

    private static LcmFamilyKeyRow RowFor(IReadOnlyList<LcmFamilyKeyRow> rows, int lcm)
        => rows.Single(r => r.Lcm == lcm);
}
