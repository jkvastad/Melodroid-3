using FluentAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class OctaveSweepTests
{
    private static readonly IReadOnlyList<Fraction> DefaultGoodFractions =
        GoodFractions.Enumerate(24, 5);

    private const double Unambiguous = 1.0 / 161.0;

    [Fact]
    public void Reference_equal_to_an_input_ratio_makes_that_input_bin_to_1_over_1()
    {
        var rows = OctaveSweep.Compute(
            new[] { 1.0 },
            DefaultGoodFractions,
            sweepStep: 0.5,
            binRadius: Unambiguous);

        var first = rows[0];
        first.ReferenceRatio.Should().Be(1.0);
        first.Cells[0].GoodFraction.Should().Be(new Fraction(1, 1));
        first.Cells[0].SignedPctDistance.Should().BeApproximately(0.0, 1e-9);
        first.FullMatch.Should().BeTrue();
        first.PostBinLcm.Should().Be(1);
    }

    [Fact]
    public void Major_triad_at_reference_1_produces_full_match()
    {
        var rows = OctaveSweep.Compute(
            new[] { 1.0, 1.25, 1.5 },
            DefaultGoodFractions,
            sweepStep: 0.5,
            binRadius: Unambiguous);

        var row = rows.Single(r => r.ReferenceRatio == 1.0);
        row.FullMatch.Should().BeTrue();
        row.Ambiguous.Should().BeFalse();
        row.PostBinLcm.Should().Be(4);
        row.Cells.Select(c => c.GoodFraction).Should().Equal(
            new Fraction(1, 1),
            new Fraction(5, 4),
            new Fraction(3, 2));
        foreach (var c in row.Cells)
        {
            c.SignedPctDistance.Should().BeApproximately(0.0, 1e-9);
        }
    }

    [Fact]
    public void Major_triad_renormalized_by_5_4_yields_minor_chord_with_lcm_5()
    {
        var step = 0.001;
        var rows = OctaveSweep.Compute(
            new[] { 1.0, 1.25, 1.5 },
            DefaultGoodFractions,
            sweepStep: step,
            binRadius: Unambiguous);

        var row = rows.Single(r => Math.Abs(r.ReferenceRatio - 1.25) < step / 2);
        row.FullMatch.Should().BeTrue();
        row.PostBinLcm.Should().Be(5);

        var gfs = row.Cells.Select(c => c.GoodFraction).OrderBy(f => f.Value).ToArray();
        gfs.Should().Equal(
            new Fraction(1, 1),
            new Fraction(6, 5),
            new Fraction(8, 5));
    }

    [Fact]
    public void Overlapping_bin_radius_marks_row_ambiguous()
    {
        var rows = OctaveSweep.Compute(
            new[] { 1.075 },
            DefaultGoodFractions,
            sweepStep: 0.5,
            binRadius: 0.05);

        var row = rows.Single(r => r.ReferenceRatio == 1.0);
        row.Ambiguous.Should().BeTrue();
        row.FullMatch.Should().BeFalse();
        row.AllInputsBinned.Should().BeTrue();

        // Candidate LCM picks the minimum across the competing denominators.
        // At radius 0.05 the input 1.075 overlaps 16/15, 10/9 and 9/8 — denominators
        // {15, 9, 8} — so the minimum-LCM selection chooses 9/8 → 8. This protects
        // against any future "pick first match" shortcut, since 16/15 is listed
        // first (sorted by value).
        row.PostBinLcm.Should().Be(8);
        row.LcmIsCandidate.Should().BeTrue();

        var cell = row.Cells[0];
        cell.Ambiguous.Should().BeTrue();
        cell.Matches.Should().HaveCountGreaterThan(1);
        cell.Matches.Select(m => m.Fraction).Should().Contain(new Fraction(16, 15));
        cell.Matches.Select(m => m.Fraction).Should().Contain(new Fraction(10, 9));
        cell.Matches.Select(m => m.Fraction).Should().Contain(new Fraction(9, 8));
    }

    [Fact]
    public void Ambiguous_cell_carries_every_matched_good_fraction()
    {
        // Regression for the 1.1140 scenario: at bin radius 0.01 the normalized
        // ratios for inputs 1.0 and 1.25 each fall inside two adjacent good-fraction
        // bins. The cell for each ambiguous input must expose both matches via
        // Matches so the table renderer can surface the competing interpretations.
        var step = 0.001;
        var rows = OctaveSweep.Compute(
            new[] { 1.0, 1.25, 1.5 },
            DefaultGoodFractions,
            sweepStep: step,
            binRadius: 0.01);

        var row = rows.Single(r => Math.Abs(r.ReferenceRatio - 1.114) < step / 2);
        row.Ambiguous.Should().BeTrue();
        row.AllInputsBinned.Should().BeTrue();

        // Candidate denominators per cell: {9, 5} × {9, 8} × {3}. Possible LCMs:
        // lcm(9,9,3)=9, lcm(9,8,3)=72, lcm(5,9,3)=45, lcm(5,8,3)=120 → min is 9.
        row.PostBinLcm.Should().Be(9);
        row.LcmIsCandidate.Should().BeTrue();

        var input1Matches = row.Cells[0].Matches.Select(m => m.Fraction).ToArray();
        input1Matches.Should().BeEquivalentTo(new[] { new Fraction(16, 9), new Fraction(9, 5) });
        row.Cells[0].Ambiguous.Should().BeTrue();

        var input125Matches = row.Cells[1].Matches.Select(m => m.Fraction).ToArray();
        input125Matches.Should().BeEquivalentTo(new[] { new Fraction(10, 9), new Fraction(9, 8) });
        row.Cells[1].Ambiguous.Should().BeTrue();

        var input15Matches = row.Cells[2].Matches.Select(m => m.Fraction).ToArray();
        input15Matches.Should().BeEquivalentTo(new[] { new Fraction(4, 3) });
        row.Cells[2].Ambiguous.Should().BeFalse();
    }

    [Fact]
    public void Ratio_outside_any_bin_excluded_from_full_match_but_others_contribute_to_lcm()
    {
        // 1.035 sits in the gap between 1/1's bin ([0.9938, 1.0062]) and 16/15's bin
        // ([1.0601, 1.0733]) at the unambiguous radius, so it must remain unbinned.
        var unbindable = 1.035;
        var rows = OctaveSweep.Compute(
            new[] { 1.0, 1.5, unbindable },
            DefaultGoodFractions,
            sweepStep: 0.5,
            binRadius: Unambiguous);

        var row = rows.Single(r => r.ReferenceRatio == 1.0);
        row.FullMatch.Should().BeFalse();
        row.Ambiguous.Should().BeFalse();
        row.AllInputsBinned.Should().BeFalse();
        row.PostBinLcm.Should().Be(IntegerMath.Lcm(1, 2));

        row.Cells[2].GoodFraction.Should().Be(default(Fraction));
        double.IsNaN(row.Cells[2].SignedPctDistance).Should().BeTrue();
    }

    [Fact]
    public void Value_just_below_octave_wraps_to_1_over_1_with_small_distance()
    {
        var rows = OctaveSweep.Compute(
            new[] { 1.999 },
            DefaultGoodFractions,
            sweepStep: 0.5,
            binRadius: Unambiguous);

        var first = rows[0];
        first.ReferenceRatio.Should().Be(1.0);
        first.Cells[0].GoodFraction.Should().Be(new Fraction(1, 1));
        first.Cells[0].SignedPctDistance.Should().BeLessThan(0.0);
        Math.Abs(first.Cells[0].SignedPctDistance).Should().BeLessThan(1.0);
        first.FullMatch.Should().BeTrue();
        first.PostBinLcm.Should().Be(1);
    }

    [Fact]
    public void Value_just_above_1_wraps_to_high_good_fraction_via_circular_distance()
    {
        var rows = OctaveSweep.Compute(
            new[] { 1.0001 },
            new[] { new Fraction(47, 24) },
            sweepStep: 0.5,
            binRadius: 0.05);

        var first = rows[0];
        first.Cells[0].GoodFraction.Should().Be(new Fraction(47, 24));
        first.Cells[0].SignedPctDistance.Should().BeGreaterThan(0.0);
        Math.Abs(first.Cells[0].SignedPctDistance).Should().BeLessThan(5.0);
        first.FullMatch.Should().BeTrue();
    }

    [Fact]
    public void Sweep_step_controls_row_count()
    {
        var rows = OctaveSweep.Compute(
            new[] { 1.0 },
            DefaultGoodFractions,
            sweepStep: 0.01,
            binRadius: Unambiguous);

        rows.Should().HaveCount(100);
        rows[0].ReferenceRatio.Should().BeApproximately(1.0, 1e-12);
        rows[^1].ReferenceRatio.Should().BeApproximately(1.99, 1e-12);
    }

    // Centered full match identification — unit tests over synthetic OctaveSweepRow
    // arrays, so the algorithm is exercised independently of bin radius / sweep step.

    private static readonly Fraction[] MajorTriad =
        { new(1, 1), new(5, 4), new(3, 2) };

    private static readonly Fraction[] MinorTriad =
        { new(1, 1), new(6, 5), new(3, 2) };

    private static OctaveSweepRow MakeFullMatchRow(
        double reference, IReadOnlyList<Fraction> signature, double[] distances)
    {
        var cells = new OctaveSweepCell[signature.Count];
        for (var i = 0; i < signature.Count; i++)
        {
            var matches = new[] { new OctaveSweepMatch(signature[i], distances[i]) };
            cells[i] = new OctaveSweepCell(signature[i], 1.0, distances[i], false, matches);
        }
        return new OctaveSweepRow(reference, cells, 4, FullMatch: true, Ambiguous: false, AllInputsBinned: true, LcmIsCandidate: false);
    }

    private static OctaveSweepRow MakeNonFullMatchRow(double reference)
    {
        var cells = new[] { new OctaveSweepCell(default, double.NaN, double.NaN, false, Array.Empty<OctaveSweepMatch>()) };
        return new OctaveSweepRow(reference, cells, null, FullMatch: false, Ambiguous: false, AllInputsBinned: false, LcmIsCandidate: false);
    }

    private static OctaveSweepRow MakeAmbiguousFullRow(
        double reference, IReadOnlyList<Fraction> signature, double[] distances)
    {
        var cells = new OctaveSweepCell[signature.Count];
        for (var i = 0; i < signature.Count; i++)
        {
            var matches = new[] { new OctaveSweepMatch(signature[i], distances[i]) };
            cells[i] = new OctaveSweepCell(signature[i], 1.0, distances[i], true, matches);
        }
        return new OctaveSweepRow(reference, cells, null, FullMatch: false, Ambiguous: true, AllInputsBinned: true, LcmIsCandidate: false);
    }

    [Fact]
    public void Centered_empty_input_returns_empty()
    {
        var centered = OctaveSweep.IdentifyCenteredFullMatches(Array.Empty<OctaveSweepRow>());
        centered.Should().BeEmpty();
    }

    [Fact]
    public void Centered_single_isolated_full_match_is_its_own_centre()
    {
        var rows = new[]
        {
            MakeNonFullMatchRow(1.00),
            MakeFullMatchRow(1.10, MajorTriad, new[] { 0.0, 0.0, 0.0 }),
            MakeNonFullMatchRow(1.20),
        };

        var centered = OctaveSweep.IdentifyCenteredFullMatches(rows);

        centered.Should().BeEquivalentTo(new[] { 1 });
    }

    [Fact]
    public void Centered_picks_row_with_smallest_max_absolute_distance()
    {
        // Three-row block; row 1 has the smallest max-|distance|.
        var rows = new[]
        {
            MakeFullMatchRow(1.10, MajorTriad, new[] {  0.50,  0.40, -0.30 }),
            MakeFullMatchRow(1.11, MajorTriad, new[] {  0.10, -0.05,  0.05 }),
            MakeFullMatchRow(1.12, MajorTriad, new[] { -0.40, -0.50,  0.30 }),
        };

        var centered = OctaveSweep.IdentifyCenteredFullMatches(rows);

        centered.Should().BeEquivalentTo(new[] { 1 });
    }

    [Fact]
    public void Centered_tie_broken_by_lowest_index()
    {
        var rows = new[]
        {
            MakeFullMatchRow(1.10, MajorTriad, new[] { 0.20,  0.00, 0.00 }),
            MakeFullMatchRow(1.11, MajorTriad, new[] { 0.20, -0.20, 0.00 }),
        };

        var centered = OctaveSweep.IdentifyCenteredFullMatches(rows);

        centered.Should().BeEquivalentTo(new[] { 0 });
    }

    [Fact]
    public void Centered_splits_blocks_on_changing_signature()
    {
        // Two back-to-back FullMatch runs with different matched-fraction tuples
        // become two distinct blocks, each producing its own centred row.
        var rows = new[]
        {
            MakeFullMatchRow(1.10, MajorTriad, new[] {  0.30,  0.20,  0.10 }),
            MakeFullMatchRow(1.11, MajorTriad, new[] {  0.00,  0.00,  0.00 }),
            MakeFullMatchRow(1.12, MinorTriad, new[] {  0.10, -0.10, -0.10 }),
            MakeFullMatchRow(1.13, MinorTriad, new[] { -0.30, -0.30, -0.30 }),
        };

        var centered = OctaveSweep.IdentifyCenteredFullMatches(rows);

        centered.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void Centered_block_ends_at_non_full_match_row()
    {
        var rows = new[]
        {
            MakeFullMatchRow(1.10, MajorTriad, new[] {  0.20, 0.10, 0.10 }),
            MakeFullMatchRow(1.11, MajorTriad, new[] {  0.00, 0.00, 0.00 }),
            MakeNonFullMatchRow(1.12),
            MakeFullMatchRow(1.13, MajorTriad, new[] { -0.05, 0.05, 0.05 }),
        };

        var centered = OctaveSweep.IdentifyCenteredFullMatches(rows);

        centered.Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void Centered_no_full_matches_returns_empty()
    {
        var rows = new[]
        {
            MakeNonFullMatchRow(1.00),
            MakeNonFullMatchRow(1.10),
            MakeNonFullMatchRow(1.20),
        };

        var centered = OctaveSweep.IdentifyCenteredFullMatches(rows);

        centered.Should().BeEmpty();
    }

    [Fact]
    public void Centered_treats_ambiguous_full_row_as_part_of_block()
    {
        // Three contiguous rows with the same signature, the middle one ambiguous.
        // The middle row has the smallest max-|distance|, so it should win even
        // though it's the ambiguous one.
        var rows = new[]
        {
            MakeFullMatchRow      (1.10, MajorTriad, new[] {  0.40,  0.30, -0.20 }),
            MakeAmbiguousFullRow  (1.11, MajorTriad, new[] {  0.05, -0.05,  0.05 }),
            MakeFullMatchRow      (1.12, MajorTriad, new[] { -0.40, -0.30,  0.20 }),
        };

        var centered = OctaveSweep.IdentifyCenteredFullMatches(rows);

        centered.Should().BeEquivalentTo(new[] { 1 });
    }

    [Fact]
    public void Centered_ambiguous_only_block_yields_its_own_centre()
    {
        var rows = new[]
        {
            MakeNonFullMatchRow(1.00),
            MakeAmbiguousFullRow(1.10, MajorTriad, new[] {  0.30,  0.20,  0.10 }),
            MakeAmbiguousFullRow(1.11, MajorTriad, new[] {  0.00,  0.00,  0.00 }),
            MakeAmbiguousFullRow(1.12, MajorTriad, new[] { -0.30, -0.20, -0.10 }),
            MakeNonFullMatchRow(1.20),
        };

        var centered = OctaveSweep.IdentifyCenteredFullMatches(rows);

        centered.Should().BeEquivalentTo(new[] { 2 });
    }

    [Fact]
    public void Centered_block_ends_at_unbinned_row_not_ambiguous_row()
    {
        // Ambiguous-only rows extend the block (since AllInputsBinned == true);
        // a fully-unbinned row closes it.
        var rows = new[]
        {
            MakeFullMatchRow    (1.10, MajorTriad, new[] {  0.20, 0.10, 0.10 }),
            MakeAmbiguousFullRow(1.11, MajorTriad, new[] {  0.00, 0.00, 0.00 }),
            MakeNonFullMatchRow (1.12),
            MakeFullMatchRow    (1.13, MajorTriad, new[] { -0.05, 0.05, 0.05 }),
        };

        var centered = OctaveSweep.IdentifyCenteredFullMatches(rows);

        centered.Should().BeEquivalentTo(new[] { 1, 3 });
    }
}
