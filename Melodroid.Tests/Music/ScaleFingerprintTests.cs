using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class ScaleFingerprintTests
{
    private const int Ktet = 12;

    // Family data under the standard good-fraction defaults, as production builds it.
    private static IReadOnlyList<LcmFamily> Families() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);

    private static IReadOnlyList<FingerprintRow> Rows(int[] chord, int minSubsetNotes) =>
        ScaleFingerprint.ContainingChord(chord, Families(), minSubsetNotes, false, false, Ktet);

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

        bool IsSingle(FingerprintRow r) => r.PB is null;

        // Single-placement rows are independent of the bridge floor — identical across both runs.
        var looseSingles = loose.Where(IsSingle).Select(r => string.Join(",", r.Keys)).OrderBy(s => s).ToList();
        var tightSingles = tight.Where(IsSingle).Select(r => string.Join(",", r.Keys)).OrderBy(s => s).ToList();
        tightSingles.Should().Equal(looseSingles);

        // Composites only shrink (monotone): every tight composite key set is also present when looser.
        var looseKeys = loose.Select(r => string.Join(",", r.Keys)).ToHashSet();
        foreach (var r in tight.Where(r => !IsSingle(r)))
            looseKeys.Should().Contain(string.Join(",", r.Keys));
    }
}
