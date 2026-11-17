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
    public void CollapseIsomorphic_triad_folds_same_key_placements_to_lowest_lcm()
    {
        var families = LcmFamilies.Compute(
            GoodFractions.Enumerate(maxSize: 24, maxPrime: 5),
            maxLcm: 24);

        var rows = Placements.FindSupersets(new[] { 0, 4, 7 }, families, ktet: 12);
        var collapsed = Placements.CollapseIsomorphic(rows);

        // Every surviving row has a unique key set.
        collapsed
            .Select(r => string.Join(",", r.Placement.Keys.OrderBy(k => k)))
            .Should().OnlyHaveUniqueItems();

        // The tightest fit is the single isomorphic pair {3@7, 4@0}, both covering {0,4,7};
        // only the lowest-LCM one (3@7) survives.
        collapsed.Where(r => r.ExtraKeysCount == 0)
            .Select(r => (r.Placement.Lcm, r.Placement.At))
            .Should().BeEquivalentTo(new[] { (3, 7) });

        // Each surviving row is the lowest LCM among all rows sharing its key set.
        foreach (var r in collapsed)
        {
            var signature = string.Join(",", r.Placement.Keys.OrderBy(k => k));
            var minLcm = rows
                .Where(o => string.Join(",", o.Placement.Keys.OrderBy(k => k)) == signature)
                .Min(o => o.Placement.Lcm);
            r.Placement.Lcm.Should().Be(minLcm);
        }
    }

    [Fact]
    public void CollapseIsomorphic_tonic_drops_duplicate_key_sets()
    {
        var families = LcmFamilies.Compute(
            GoodFractions.Enumerate(maxSize: 24, maxPrime: 5),
            maxLcm: 24);

        var rows = Placements.FindSupersets(new[] { 0 }, families, ktet: 12);
        var collapsed = Placements.CollapseIsomorphic(rows);

        // The tonic's exhaustive superset sweep repeats key sets across isomorphic families,
        // so collapsing strictly reduces the row count.
        collapsed.Count.Should().BeLessThan(rows.Count);

        // Every surviving row covers a distinct key set.
        collapsed
            .Select(r => string.Join(",", r.Placement.Keys.OrderBy(k => k)))
            .Should().OnlyHaveUniqueItems();

        // Each survivor is the lowest LCM among all rows sharing its key set.
        foreach (var r in collapsed)
        {
            var signature = string.Join(",", r.Placement.Keys.OrderBy(k => k));
            var minLcm = rows
                .Where(o => string.Join(",", o.Placement.Keys.OrderBy(k => k)) == signature)
                .Min(o => o.Placement.Lcm);
            r.Placement.Lcm.Should().Be(minLcm);
        }
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
    public void CompareSupersets_partitions_two_key_sets_by_placement_identity()
    {
        var families = LcmFamilies.Compute(
            GoodFractions.Enumerate(maxSize: 24, maxPrime: 5),
            maxLcm: 24);

        var keysA = new[] { 0, 4, 7 };
        var keysB = new[] { 0, 3, 7 };
        var supersetsA = Placements.FindSupersets(keysA, families, ktet: 12);
        var supersetsB = Placements.FindSupersets(keysB, families, ktet: 12);

        var (common, onlyA, onlyB) = Placements.CompareSupersets(supersetsA, supersetsB);

        static (int, int) Id(KeySupersetRow r) => (r.Placement.Lcm, r.Placement.At);
        var idsA = supersetsA.Select(Id).ToHashSet();
        var idsB = supersetsB.Select(Id).ToHashSet();

        // Common ∪ OnlyA reconstructs A's identity set; Common ∪ OnlyB reconstructs B's.
        common.Concat(onlyA).Select(Id).Should().BeEquivalentTo(idsA);
        common.Concat(onlyB).Select(Id).Should().BeEquivalentTo(idsB);

        // Common identities live in both inputs; OnlyA/OnlyB in exactly one.
        common.Select(Id).Should().OnlyContain(id => idsA.Contains(id) && idsB.Contains(id));
        onlyA.Select(Id).Should().OnlyContain(id => !idsB.Contains(id));
        onlyB.Select(Id).Should().OnlyContain(id => !idsA.Contains(id));

        // Every common placement's keys are a superset of both requested sets.
        common.Should().OnlyContain(r =>
            keysA.All(key => r.Placement.Keys.Contains(key)) &&
            keysB.All(key => r.Placement.Keys.Contains(key)));

        common.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(15, 7, 12, 3)]
    [InlineData(15, 11, 12, 7)]
    [InlineData(15, 4, 12, 0)]
    public void CollapsingKey_lcm15_in_12tet_is_at_plus_8(int lcm, int at, int ktet, int expected)
    {
        var placement = new Placement(lcm, at, new[] { 0 });

        Placements.CollapsingKey(placement, ktet).Should().Be(expected);
    }

    [Theory]
    [InlineData(20, 4, 12, 7)] // 20@4 is the subset of 15@11, collapses on key 7
    [InlineData(20, 0, 12, 3)]
    [InlineData(20, 9, 12, 0)]
    public void CollapsingKey_lcm20_in_12tet_is_at_plus_3(int lcm, int at, int ktet, int expected)
    {
        var placement = new Placement(lcm, at, new[] { 0 });

        Placements.CollapsingKey(placement, ktet).Should().Be(expected);
    }

    [Theory]
    [InlineData(24, 0, 12)] // non-15/20 family
    [InlineData(15, 7, 19)] // non-12 ktet
    [InlineData(20, 7, 19)] // non-12 ktet
    public void CollapsingKey_returns_null_off_the_12tet_lcm15_or_lcm20_case(int lcm, int at, int ktet)
    {
        var placement = new Placement(lcm, at, new[] { 0 });

        Placements.CollapsingKey(placement, ktet).Should().BeNull();
    }

    [Fact]
    public void DropCollapsed_major_triad_keeps_15at7_and_all_24()
    {
        var chord = new[] { 0, 4, 7 };

        // DropCollapsed reads only Lcm/At (via CollapsingKey), so the key sets here are immaterial.
        // The three lcm-15 placements containing the triad are 15@4 / 15@7 / 15@11; the three lcm-24
        // ones are 24@0 / 24@5 / 24@7.
        var placements = new List<Placement>
        {
            new(15, 4, new[] { 0 }), new(15, 7, new[] { 0 }), new(15, 11, new[] { 0 }),
            new(24, 0, new[] { 0 }), new(24, 5, new[] { 0 }), new(24, 7, new[] { 0 }),
        };
        var kept = Placements.DropCollapsed(placements, chord, ktet: 12);

        // 15@4 (collapses on 0) and 15@11 (collapses on 7) are dropped; 15@7 (collapses on 3,
        // not in the chord) survives alongside every lcm-24 row.
        kept
            .Select(p => (p.Lcm, p.At))
            .Should().Equal(new[]
            {
                (15, 7),
                (24, 0), (24, 5), (24, 7),
            });
    }

    [Fact]
    public void DropCollapsed_drops_a_collapsed_lcm20_but_keeps_a_stable_one()
    {
        // 20@4 collapses on key (4+3) mod 12 = 7; 20@0 collapses on key 3.
        var placements = new List<Placement>
        {
            new(20, 4, new[] { 0, 2, 4, 7, 8, 11 }),
            new(20, 0, new[] { 0 }),
        };
        var chord = new[] { 0, 4, 7 }; // contains 7 (drops 20@4), not 3 (keeps 20@0)

        var kept = Placements.DropCollapsed(placements, chord, ktet: 12);

        kept.Select(p => (p.Lcm, p.At)).Should().Equal(new[] { (20, 0) });
    }

    [Fact]
    public void StableFifteen_drops_only_the_collapsing_key_of_lcm15_rows()
    {
        // 15@3's full key set is 0 1 3 4 6 8 11; a lcm-24 placement alongside it is left untouched.
        var fifteenFull = new[] { 0, 1, 3, 4, 6, 8, 11 };
        var twentyFourKeys = new[] { 0, 2, 4, 5, 7, 9, 11 };
        var placements = new List<Placement>
        {
            new(15, 3, fifteenFull),
            new(24, 0, twentyFourKeys),
        };

        var stable = Placements.StableFifteen(placements, ktet: 12);

        // 15@3's collapsing key is (3+8) mod 12 = 11: the full 0 1 3 4 6 8 11 becomes the stable
        // 0 1 3 4 6 8.
        var fifteen = stable.Single(p => p.Lcm == 15);
        fifteen.At.Should().Be(3);
        fifteen.Keys.Should().BeEquivalentTo(new[] { 0, 1, 3, 4, 6, 8 });
        fifteen.Keys.Should().NotContain(11);

        // The non-lcm-15 placement is untouched (same key set as before).
        stable.Single(p => p.Lcm == 24).Keys.Should().Equal(twentyFourKeys);
    }

    [Fact]
    public void StableFifteen_leaves_lcm20_placements_untouched()
    {
        // CollapsingKey now fires for lcm-20, but 15s is an lcm-15 concept — lcm-20 rows pass through.
        var placements = new List<Placement> { new(20, 4, new[] { 0, 2, 4, 7, 8, 11 }) };

        Placements.StableFifteen(placements, ktet: 12)[0].Keys.Should().Equal(0, 2, 4, 7, 8, 11);
    }

    [Fact]
    public void StableFifteen_leaves_non_12tet_placements_untouched()
    {
        // CollapsingKey is null off the 12-tet lcm-15 case, so nothing is dropped.
        var placements = new List<Placement> { new(15, 7, new[] { 0, 3, 7 }) };

        Placements.StableFifteen(placements, ktet: 19)[0].Keys.Should().Equal(0, 3, 7);
    }

    private static LcmFamily FamilyFor(int lcm)
    {
        var goodFractions = GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);
        var families = LcmFamilies.Compute(goodFractions, maxLcm: 24);
        return families.Single(f => f.Lcm == lcm);
    }
}
