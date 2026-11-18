using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class ChordMelodyTests
{
    private const int Ktet = 12;

    // Family data under the standard good-fraction defaults, as production builds it.
    private static IReadOnlyList<LcmFamily> Families() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);

    private static IReadOnlyList<ChordMelodyRow> Rows(int[] chord, int minSubsetNotes) =>
        ChordMelody.ContainingChord(chord, Families(), minSubsetNotes, false, false, Ktet);

    private static IReadOnlyList<ChordMelodyRow> Rows(
        int[] chord, int minSubsetNotes, bool collapseIsomorphic, bool poolMaximal) =>
        ChordMelody.ContainingChord(chord, Families(), minSubsetNotes, collapseIsomorphic, poolMaximal, Ktet);

    private static bool IsSingle(ChordMelodyRow r) => r.PB is null;

    [Theory]
    [InlineData(new[] { 0, 4, 7 })]
    [InlineData(new[] { 0, 3, 7 })]
    [InlineData(new[] { 2, 6, 9 })]
    public void EveryRowContainsTheChord(int[] chord)
    {
        var chordSet = chord.ToHashSet();
        foreach (var row in Rows(chord, 4))
            chordSet.IsSubsetOf(row.Keys).Should().BeTrue($"row {string.Join(",", row.Keys)} must contain the chord");
    }

    [Fact]
    public void MajorTriadYieldsTheMajorScaleAsSingle24Placement()
    {
        var rows = Rows(new[] { 0, 4, 7 }, 4);

        // major rooted on 0 = the lcm-24 placement 24@0, keys {0,2,4,5,7,9,11}, as a single placement.
        var major = rows.Should().ContainSingle(r =>
            r.PA.Lcm == 24 && r.PA.At == 0 && r.PB == null).Subject;
        major.Keys.Should().Equal(0, 2, 4, 5, 7, 9, 11);

        // The named-scale catalog agrees this key set is major @0.
        NamedScales.Match(major.Keys, Ktet).Should().Be(("major", 0));
    }

    [Fact]
    public void CompositeRowKeysEqualTheStableUnionOfItsConstituents()
    {
        var composites = Rows(new[] { 0, 4, 7 }, 4).Where(r => r.PB is not null);
        composites.Should().NotBeEmpty();

        foreach (var row in composites)
        {
            var expected = CompositeScales.UnionKeys(row.PA, row.PB!.Value, stable: true, Ktet);
            row.Keys.Should().Equal(expected);
        }
    }

    [Fact]
    public void NoTwoRowsShareAKeySet()
    {
        var rows = Rows(new[] { 0, 4, 7 }, 4);
        var signatures = rows.Select(r => string.Join(",", r.Keys)).ToList();
        signatures.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void RaisingMinSubsetNotesOnlyRemovesCompositeRowsAndKeepsSinglePlacements()
    {
        var chord = new[] { 0, 4, 7 };
        var loose = Rows(chord, 2);
        var tight = Rows(chord, 6);

        // Single-placement rows are independent of the bridge floor — identical across both runs.
        var looseSingles = loose.Where(IsSingle).Select(r => string.Join(",", r.Keys)).OrderBy(s => s).ToList();
        var tightSingles = tight.Where(IsSingle).Select(r => string.Join(",", r.Keys)).OrderBy(s => s).ToList();
        tightSingles.Should().Equal(looseSingles);

        // Composites only shrink (monotone): every tight composite key set is also present when looser.
        var looseKeys = loose.Select(r => string.Join(",", r.Keys)).ToHashSet();
        foreach (var r in tight.Where(r => !IsSingle(r)))
            looseKeys.Should().Contain(string.Join(",", r.Keys));
    }

    [Fact]
    public void PoolMaximalKeepsAnAntichainWithinEachCategory()
    {
        var rows = Rows(new[] { 0, 4, 7 }, 4, collapseIsomorphic: false, poolMaximal: true);

        void AssertAntichain(IReadOnlyList<ChordMelodyRow> partition)
        {
            // b.Keys.Count > a.Keys.Count already excludes a == b (equal key sets are deduped away).
            foreach (var a in partition)
                foreach (var b in partition)
                    if (b.Keys.Count > a.Keys.Count)
                        new HashSet<int>(a.Keys).IsSubsetOf(b.Keys).Should().BeFalse(
                            $"{string.Join(",", a.Keys)} must not be a subset of same-category {string.Join(",", b.Keys)}");
        }

        AssertAntichain(rows.Where(IsSingle).ToList());
        AssertAntichain(rows.Where(r => !IsSingle(r)).ToList());
    }

    [Fact]
    public void PoolMaximalDropsTheChordExactSinglePlacement()
    {
        var chord = new[] { 0, 4, 7 };

        // Without the filter the chord itself appears as a single placement (keys == the chord).
        Rows(chord, 4).Should().Contain(r => IsSingle(r) && r.Keys.SequenceEqual(chord));

        // A larger single placement contains it, so per-category maximal drops it.
        Rows(chord, 4, collapseIsomorphic: false, poolMaximal: true)
            .Should().NotContain(r => r.Keys.SequenceEqual(chord));
    }

    [Fact]
    public void CollapseIsomorphicRelabelsSinglePlacementsToTheCanonicalRepresentative()
    {
        var families = Families();
        var canonical = ChordProgressions.CanonicalPlacements(families, Ktet);
        var rows = ChordMelody.ContainingChord(
            new[] { 0, 4, 7 }, families, 4, collapseIsomorphic: true, poolMaximal: false, Ktet);

        // Compare by (Lcm, At): Placement is a record struct whose Keys list compares by reference, so
        // the fresh canonical map here holds different list instances than the one inside ContainingChord.
        foreach (var row in rows.Where(IsSingle))
        {
            var rep = ChordProgressions.CanonicalRepresentative(canonical, row.Keys);
            (row.PA.Lcm, row.PA.At).Should().Be((rep.Lcm, rep.At));
        }
    }
}
