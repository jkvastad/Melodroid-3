using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class SuperpositionsTests
{
    private static IReadOnlyList<LcmFamily> DefaultFamilies() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(maxSize: 24, maxPrime: 5), maxLcm: 24);

    // Independent oracle: the keys a placement L@a occupies on a k-tet keyboard.
    private static HashSet<int> KeysOf(int lcm, int at, int ktet)
    {
        var family = DefaultFamilies().Single(f => f.Lcm == lcm);
        return new HashSet<int>(Placements.Compute(family, at, ktet).Keys);
    }

    // Independent oracle: lcm of a set of integers via a locally reimplemented gcd/lcm.
    private static int LcmOf(IEnumerable<int> values)
    {
        static int Gcd(int a, int b)
        {
            a = Math.Abs(a);
            b = Math.Abs(b);
            while (b != 0) (a, b) = (b, a % b);
            return a == 0 ? 1 : a;
        }
        return values.Aggregate(1, (acc, v) => acc / Gcd(acc, v) * v);
    }

    [Fact]
    public void Major_scale_is_two_LCM8_placements_with_no_extras()
    {
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 8, maxBlockLcm: 12, maxResults: 50, uniqueReference: false);

        // The headline insight: C major scale = 8@0 + 8@5, exactly, no leftover keys.
        rows.Should().Contain(s =>
            s.Pieces.Count == 2 &&
            s.ExtraKeys.Count == 0 &&
            s.Pieces.Any(p => p.Lcm == 8 && p.At == 0) &&
            s.Pieces.Any(p => p.Lcm == 8 && p.At == 5));
    }

    [Fact]
    public void Natural_minor_is_two_LCM8_placements_with_no_extras()
    {
        var target = new[] { 0, 1, 3, 5, 6, 8, 10 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 8, maxBlockLcm: 12, maxResults: 50, uniqueReference: false);

        rows.Should().Contain(s =>
            s.Pieces.Count == 2 &&
            s.ExtraKeys.Count == 0 &&
            s.Pieces.Any(p => p.Lcm == 8 && p.At == 1) &&
            s.Pieces.Any(p => p.Lcm == 8 && p.At == 6));
    }

    [Fact]
    public void Target_equal_to_a_family_placement_yields_trivial_single_piece_cover()
    {
        // The C major scale is exactly 24@0; the size-1 cover must appear first, no extras.
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 50, uniqueReference: false);

        rows.Should().NotBeEmpty();
        rows[0].Pieces.Should().ContainSingle();
        rows[0].Pieces[0].Lcm.Should().Be(24);
        rows[0].Pieces[0].At.Should().Be(0);
        rows[0].ExtraKeys.Should().BeEmpty();
        rows[0].DisjointOnTarget.Should().BeTrue();
    }

    [Fact]
    public void Harmonic_minor_has_no_single_piece_cover_because_it_is_not_a_family()
    {
        // A♯ harmonic minor {0,2,3,5,7,8,11} matches no LCM family placement.
        var target = new[] { 0, 2, 3, 5, 7, 8, 11 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference: false);

        rows.Should().NotContain(s => s.Pieces.Count == 1);
    }

    [Fact]
    public void Every_returned_cover_actually_covers_the_target()
    {
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };
        var targetSet = new HashSet<int>(target);

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference: false);

        rows.Should().NotBeEmpty();
        foreach (var s in rows)
        {
            var union = new HashSet<int>();
            foreach (var p in s.Pieces) union.UnionWith(KeysOf(p.Lcm, p.At, ktet: 12));
            targetSet.IsSubsetOf(union).Should().BeTrue();
        }
    }

    [Fact]
    public void Every_returned_cover_is_irredundant()
    {
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };
        var targetSet = new HashSet<int>(target);

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference: false);

        foreach (var s in rows)
        {
            // Removing any one piece must drop at least one target key.
            for (var i = 0; i < s.Pieces.Count; i++)
            {
                var withoutI = new HashSet<int>();
                for (var j = 0; j < s.Pieces.Count; j++)
                {
                    if (j == i) continue;
                    withoutI.UnionWith(KeysOf(s.Pieces[j].Lcm, s.Pieces[j].At, ktet: 12));
                }
                targetSet.IsSubsetOf(withoutI).Should().BeFalse();
            }
        }
    }

    [Fact]
    public void ExtraKeys_are_exactly_the_union_keys_outside_the_target()
    {
        var target = new[] { 0, 1, 3, 5, 6, 8, 10 };
        var targetSet = new HashSet<int>(target);

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference: false);

        foreach (var s in rows)
        {
            var union = new HashSet<int>();
            foreach (var p in s.Pieces) union.UnionWith(KeysOf(p.Lcm, p.At, ktet: 12));
            var expectedExtra = union.Where(k => !targetSet.Contains(k)).OrderBy(k => k);
            s.ExtraKeys.Should().Equal(expectedExtra);
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    public void MinBlockLcm_excludes_smaller_families(int minBlockLcm)
    {
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm, maxBlockLcm: 24, maxResults: 200, uniqueReference: false);

        rows.SelectMany(s => s.Pieces).Should().OnlyContain(p => p.Lcm >= minBlockLcm);
    }

    [Fact]
    public void MaxBlockLcm_excludes_larger_families()
    {
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 12, maxResults: 200, uniqueReference: false);

        rows.SelectMany(s => s.Pieces).Should().OnlyContain(p => p.Lcm <= 12);
        // With blocks capped at 12 the trivial 24@0 cover cannot appear.
        rows.Should().NotContain(s => s.Pieces.Any(p => p.Lcm == 24));
    }

    [Fact]
    public void Results_are_sorted_by_piece_count_then_extra_count()
    {
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference: false);

        for (var i = 1; i < rows.Count; i++)
        {
            var prev = (rows[i - 1].Pieces.Count, rows[i - 1].ExtraKeys.Count);
            var cur = (rows[i].Pieces.Count, rows[i].ExtraKeys.Count);
            Comparer<(int, int)>.Default.Compare(cur, prev).Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public void MaxResults_caps_the_output_and_reports_truncation()
    {
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };

        var (rows, truncated) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 3, uniqueReference: false);

        rows.Should().HaveCount(3);
        truncated.Should().BeTrue();
    }

    [Fact]
    public void Unique_reference_covers_all_share_one_anchor()
    {
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference: true);

        rows.Should().NotBeEmpty();
        foreach (var s in rows)
        {
            s.Reference.Should().NotBeNull();
            s.Pieces.Should().OnlyContain(p => p.At == s.Reference!.Value);
        }
    }

    [Fact]
    public void Unique_reference_surfaces_isomorphic_aliases_at_shifted_anchors()
    {
        // {0,4,7} is both 4@0 and its isomorphic alias 3@7 (LCM-3 placed at key 7).
        // Unique-reference enumeration must offer both as single-piece covers, each tagged
        // with its own reference key — the alias is what lets a piece sit at reference 7.
        var target = new[] { 0, 4, 7 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference: true);

        rows.Should().Contain(s =>
            s.Reference == 0 && s.Pieces.Count == 1 && s.Pieces[0].Lcm == 4 && s.Pieces[0].At == 0);
        rows.Should().Contain(s =>
            s.Reference == 7 && s.Pieces.Count == 1 && s.Pieces[0].Lcm == 3 && s.Pieces[0].At == 7);
    }

    [Fact]
    public void Mixed_anchor_cover_appears_only_under_any_reference()
    {
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };

        var (anyRows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 8, maxBlockLcm: 12, maxResults: 200, uniqueReference: false);
        var (uniqueRows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 8, maxBlockLcm: 12, maxResults: 200, uniqueReference: true);

        // 8@0 + 8@5 roots its two families on different keys, so it is a valid any-reference
        // cover but cannot be a unique-reference one.
        anyRows.Should().Contain(s =>
            s.Pieces.Count == 2 &&
            s.Pieces.Any(p => p.Lcm == 8 && p.At == 0) &&
            s.Pieces.Any(p => p.Lcm == 8 && p.At == 5));
        uniqueRows.Should().NotContain(s =>
            s.Pieces.Count == 2 &&
            s.Pieces.Any(p => p.Lcm == 8 && p.At == 0) &&
            s.Pieces.Any(p => p.Lcm == 8 && p.At == 5));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Trivial_single_piece_cover_appears_in_both_modes(bool uniqueReference)
    {
        // The C major scale is exactly 24@0 — a single piece is trivially unique-reference.
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference);

        rows.Should().Contain(s => s.Pieces.Count == 1 && s.Pieces[0].Lcm == 24 && s.Pieces[0].At == 0);
    }

    [Fact]
    public void Any_reference_rows_carry_no_reference_tag()
    {
        var target = new[] { 0, 4, 7 };

        var (rows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference: false);

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(s => s.Reference == null);
    }

    [Fact]
    public void Unique_reference_combined_lcm_is_lcm_of_piece_lcms_and_null_under_any_reference()
    {
        var target = new[] { 0, 2, 4, 5, 7, 9, 11 };

        var (uniqueRows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference: true);

        uniqueRows.Should().NotBeEmpty();
        foreach (var s in uniqueRows)
            s.CombinedLcm.Should().Be(LcmOf(s.Pieces.Select(p => p.Lcm)));

        var (anyRows, _) = Superpositions.Enumerate(
            target, DefaultFamilies(), ktet: 12, minBlockLcm: 2, maxBlockLcm: 24, maxResults: 200, uniqueReference: false);

        anyRows.Should().NotBeEmpty();
        anyRows.Should().OnlyContain(s => s.CombinedLcm == null);
    }
}
