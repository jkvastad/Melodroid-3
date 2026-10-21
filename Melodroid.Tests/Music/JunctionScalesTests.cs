using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class JunctionScalesTests
{
    private const int Ktet = 12;

    // Family data under the standard good-fraction defaults, as production builds it.
    private static IReadOnlyList<LcmFamily> Families() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);

    private static IReadOnlyList<int> PlacementKeys(int lcm, int at)
    {
        var family = Families().Single(f => f.Lcm == lcm);
        return Placements.Compute(family, at, Ktet).Keys.OrderBy(k => k).ToList();
    }

    // Independent oracle: are key sets a and b transpositions of one another (some shift maps a→b)?
    // Reimplemented from scratch rather than reusing Chords.Canonicalize (the production dedup key).
    private static bool AreTranspositions(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        if (a.Count != b.Count) return false;
        var bSet = new HashSet<int>(b);
        for (var t = 0; t < Ktet; t++)
            if (a.Select(k => (k + t) % Ktet).ToHashSet().SetEquals(bSet)) return true;
        return false;
    }

    // harm@9 = {0 2 4 5 8 9 11} derived independently: stable(15@11) ∪ 24@0 with 15@11's collapse key
    // (11+8) mod 12 = 7 dropped from the union. 24@0 independently carries 7, so the subtraction must
    // apply to the finished union, not just the lcm-15 constituent.
    private static readonly int[] HarmKeys = { 0, 2, 4, 5, 8, 9, 11 };

    [Fact]
    public void HarmonicMinor_FallsOutOfThe15At11_24At0_Pair_InStableForm()
    {
        var collapse = (11 + 8) % Ktet; // 7
        var expected = PlacementKeys(15, 11)
            .Concat(PlacementKeys(24, 0))
            .Where(k => k != collapse)
            .Distinct()
            .OrderBy(k => k)
            .ToList();
        expected.Should().Equal(HarmKeys); // two independent oracles agree

        var scales = JunctionScales.Enumerate(
            Families(), minSubsetNotes: 4, stable: true, dedupeTransposition: false);

        // Order() sorts the outer pair by (Lcm, At), so 15@11 is P_A and 24@0 is P_B.
        scales.Should().Contain(s =>
            s.PA.Lcm == 15 && s.PA.At == 11 &&
            s.PB.Lcm == 24 && s.PB.At == 0 &&
            s.S.Lcm == 18 &&
            s.Keys.SequenceEqual(expected));
    }

    [Fact]
    public void StableForm_DropsTheCollapseKeyFromTheUnion_RawKeepsIt()
    {
        bool IsHarmPair(JunctionScale s) =>
            s.PA.Lcm == 15 && s.PA.At == 11 && s.PB.Lcm == 24 && s.PB.At == 0;

        var stable = JunctionScales.Enumerate(
            Families(), minSubsetNotes: 4, stable: true, dedupeTransposition: false);
        var raw = JunctionScales.Enumerate(
            Families(), minSubsetNotes: 4, stable: false, dedupeTransposition: false);

        // Key 7 is 15@11's collapsing key. Stable form removes it; raw form (= plain union) keeps it.
        stable.Where(IsHarmPair).Should().NotBeEmpty();
        stable.Where(IsHarmPair).Should().OnlyContain(s => !s.Keys.Contains(7));
        raw.Where(IsHarmPair).Should().OnlyContain(s => s.Keys.Contains(7));
    }

    [Fact]
    public void Blues_ConstituentsAreEnumerated_AsTheUnion_WhichIsASupersetOfBlues()
    {
        // blues@4 = {1 4 6 7 8 11} is a *manual sub-selection* of this union, NOT a formula output:
        // 24@9 ∪ 24@2 = {1 2 4 6 7 8 9 11} and neither 24 collapses, so no rule drops keys 2 and 9.
        var union = PlacementKeys(24, 2)
            .Concat(PlacementKeys(24, 9))
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        var scales = JunctionScales.Enumerate(
            Families(), minSubsetNotes: 4, stable: true, dedupeTransposition: false);

        scales.Should().Contain(s =>
            s.PA.Lcm == 24 && s.PA.At == 2 &&
            s.PB.Lcm == 24 && s.PB.At == 9 &&
            s.Keys.SequenceEqual(union));

        var blues = new[] { 1, 4, 6, 7, 8, 11 };
        blues.Should().OnlyContain(k => union.Contains(k)); // blues ⊂ the emitted union
    }

    [Fact]
    public void DedupeTransposition_KeepsOneRepresentativePerNecklace()
    {
        var deduped = JunctionScales.Enumerate(Families(), minSubsetNotes: 4, dedupeTransposition: true);

        // No two distinct deduped scales are transpositions of one another.
        for (var i = 0; i < deduped.Count; i++)
            for (var j = i + 1; j < deduped.Count; j++)
                AreTranspositions(deduped[i].Keys, deduped[j].Keys).Should().BeFalse();

        // Harmonic minor survives dedupe (as some transposition of {0 2 4 5 8 9 11}).
        deduped.Should().Contain(s => AreTranspositions(s.Keys, HarmKeys));

        // Without dedupe the harm necklace recurs across anchors.
        var raw = JunctionScales.Enumerate(Families(), minSubsetNotes: 4, dedupeTransposition: false);
        raw.Count(s => AreTranspositions(s.Keys, HarmKeys)).Should().BeGreaterThan(1);
    }

    [Fact]
    public void MinSubsetNotes_GatesTheBridgeSize()
    {
        // harm's bridge S = 18@2 has 6 keys, so requiring 7 removes it. (NotContain takes an
        // Expression predicate, so the 15@11/24@0 test is inlined rather than a local function.)
        var at7 = JunctionScales.Enumerate(Families(), minSubsetNotes: 7, dedupeTransposition: false);
        at7.Should().NotContain(s =>
            s.PA.Lcm == 15 && s.PA.At == 11 && s.PB.Lcm == 24 && s.PB.At == 0);

        // Lowering the bar admits more bridges, so strictly more scale rows.
        var at2 = JunctionScales.Enumerate(Families(), minSubsetNotes: 2, dedupeTransposition: false);
        var at4 = JunctionScales.Enumerate(Families(), minSubsetNotes: 4, dedupeTransposition: false);
        at2.Count.Should().BeGreaterThan(at4.Count);
    }

    [Fact]
    public void Declutters_ShrinkTheLabelSpace_WithoutRemovingTheHarmScale()
    {
        var raw = JunctionScales.Enumerate(Families(), minSubsetNotes: 4, dedupeTransposition: false);
        var pooled = JunctionScales.Enumerate(
            Families(), minSubsetNotes: 4, poolMaximal: true, dedupeTransposition: false);
        var collapsed = JunctionScales.Enumerate(
            Families(), minSubsetNotes: 4, collapseIsomorphic: true, dedupeTransposition: false);

        pooled.Count.Should().BeLessThan(raw.Count);
        collapsed.Count.Should().BeLessThanOrEqualTo(raw.Count);

        pooled.Should().Contain(s => AreTranspositions(s.Keys, HarmKeys));
        collapsed.Should().Contain(s => AreTranspositions(s.Keys, HarmKeys));
    }
}
