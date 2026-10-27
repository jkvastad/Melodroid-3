using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class NamedScalesTests
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
    // Reimplemented from scratch rather than reusing production helpers.
    private static bool AreTranspositions(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        if (a.Count != b.Count) return false;
        var bSet = new HashSet<int>(b);
        for (var t = 0; t < Ktet; t++)
            if (a.Select(k => (k + t) % Ktet).ToHashSet().SetEquals(bSet)) return true;
        return false;
    }

    // Independent oracle: is some transposition of `small` a subset of `big`?
    private static bool IsTransposedSubset(IReadOnlyList<int> small, IReadOnlyList<int> big)
    {
        var bigSet = new HashSet<int>(big);
        for (var t = 0; t < Ktet; t++)
            if (small.All(k => bigSet.Contains((k + t) % Ktet))) return true;
        return false;
    }

    [Fact]
    public void Catalog_HasTheSixNamedScales_EachSortedAndRootedOnZero()
    {
        NamedScales.Catalog.Select(s => s.Name).Should().Equal(
            "major", "harmonic minor", "melodic minor", "bebop dominant", "harmonic major", "major blues");

        foreach (var scale in NamedScales.Catalog)
        {
            scale.Necklace.Should().BeInAscendingOrder();
            scale.Necklace.Should().OnlyHaveUniqueItems();
            scale.Necklace.Should().Contain(0, "a root-oriented necklace starts on key 0");
            scale.Necklace.Should().OnlyContain(k => k >= 0 && k < Ktet);
        }
    }

    [Fact]
    public void Catalog_ScalesAreDistinctTranspositionClasses()
    {
        var catalog = NamedScales.Catalog;
        for (var i = 0; i < catalog.Count; i++)
            for (var j = i + 1; j < catalog.Count; j++)
                AreTranspositions(catalog[i].Necklace, catalog[j].Necklace).Should().BeFalse(
                    $"{catalog[i].Name} and {catalog[j].Name} must be different necklaces");
    }

    [Fact]
    public void Major_IsTheLcm24Placement()
    {
        var major = NamedScales.Catalog.Single(s => s.Name == "major").Necklace;
        AreTranspositions(major, PlacementKeys(24, 0)).Should().BeTrue();
    }

    [Fact]
    public void JunctionScales_AppearAsTranspositionClassesOfProductionOutputs()
    {
        // The four non-major, non-blues scales are genuine subset-principle outputs (first-principles
        // provenance guard): each is a transposition class among JunctionScales.Enumerate at the
        // --min-subset-notes 4 floor the doc uses.
        var produced = JunctionScales
            .Enumerate(Families(), minSubsetNotes: 4, stable: true, dedupeTransposition: false)
            .Select(s => s.Keys)
            .ToList();

        foreach (var name in new[] { "harmonic minor", "melodic minor", "bebop dominant", "harmonic major" })
        {
            var necklace = NamedScales.Catalog.Single(s => s.Name == name).Necklace;
            produced.Should().Contain(keys => AreTranspositions(keys, necklace),
                $"{name} should be derivable as a junction scale");
        }
    }

    [Fact]
    public void MajorBlues_IsASubSelectionOfTheBebopDominantNecklace()
    {
        // Major blues is NOT a direct union output — it is the documented sub-selection of the bebop
        // dominant union (24@0 ∪ 24@5 minus the lcm-24 roots), so it sits inside the bebop necklace.
        var blues = NamedScales.Catalog.Single(s => s.Name == "major blues").Necklace;
        var bebop = NamedScales.Catalog.Single(s => s.Name == "bebop dominant").Necklace;

        IsTransposedSubset(blues, bebop).Should().BeTrue();
        AreTranspositions(blues, bebop).Should().BeFalse("blues drops two keys from bebop dominant");
    }

    [Fact]
    public void ContainingChord_EveryRowContainsTheChord()
    {
        foreach (var chord in new[] { new[] { 0, 4, 7 }, new[] { 0, 3, 7 }, new[] { 2 }, new[] { 0, 1 } })
        {
            var chordSet = new HashSet<int>(chord);
            var rows = NamedScales.ContainingChord(chord);
            rows.Should().OnlyContain(r => chordSet.IsSubsetOf(r.Keys));
        }
    }

    [Fact]
    public void ContainingChord_MatchesBruteForceOverAllShifts()
    {
        // Independent recomputation: for each scale and each of the 12 shifts, keep the ones whose
        // transposed key set contains the chord. The production result must agree exactly.
        var chord = new[] { 0, 4, 7 };
        var chordSet = new HashSet<int>(chord);

        var expected = new List<(string, int)>();
        foreach (var scale in NamedScales.Catalog)
            for (var a = 0; a < Ktet; a++)
            {
                var keys = scale.Necklace.Select(k => (k + a) % Ktet).ToHashSet();
                if (chordSet.IsSubsetOf(keys)) expected.Add((scale.Name, a));
            }

        var actual = NamedScales.ContainingChord(chord).Select(r => (r.Name, r.Anchor)).ToList();
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ContainingChord_MajorTriad_YieldsMajorRootedScales()
    {
        // The C major triad {0,4,7} must appear in C major (24@0), and the catalog's major-third
        // scales reach it; a minor triad instead opens the minor-third scales. Assert concrete,
        // musically obvious members without over-specifying the full set.
        var majorRows = NamedScales.ContainingChord(new[] { 0, 4, 7 });
        majorRows.Should().Contain(r => r.Name == "major" && r.Anchor == 0);
        majorRows.Should().Contain(r => r.Name == "bebop dominant" && r.Anchor == 0);
        majorRows.Should().Contain(r => r.Name == "major blues" && r.Anchor == 0);

        var minorRows = NamedScales.ContainingChord(new[] { 0, 3, 7 });
        minorRows.Should().Contain(r => r.Name == "harmonic minor" && r.Anchor == 0);
        minorRows.Should().Contain(r => r.Name == "melodic minor" && r.Anchor == 0);
    }

    [Fact]
    public void Match_ExactCatalogNecklace_ReturnsItsNameAndAnchor()
    {
        // The whole transposed necklace (not merely a subset) must coincide with the key set.
        var harmAt9 = NamedScales.Catalog.Single(s => s.Name == "harmonic minor").Necklace
            .Select(k => (k + 9) % Ktet).ToList();   // harm@9 = {0 2 3 5 7 8 11}

        NamedScales.Match(harmAt9).Should().Be(("harmonic minor", 9));
    }

    [Fact]
    public void Match_MajorScaleAtEveryAnchor_RoundTrips()
    {
        var major = NamedScales.Catalog.Single(s => s.Name == "major").Necklace;
        for (var a = 0; a < Ktet; a++)
        {
            var keys = major.Select(k => (k + a) % Ktet).ToList();
            NamedScales.Match(keys).Should().Be(("major", a));
        }
    }

    [Fact]
    public void Match_SubsetButNotWholeScale_ReturnsNull()
    {
        // A triad sits inside many scales (ContainingChord finds them) but equals none, so Match fails.
        NamedScales.Match(new[] { 0, 4, 7 }).Should().BeNull();
    }

    [Fact]
    public void Match_NonCatalogKeySet_ReturnsNull()
    {
        // The whole-tone hexad {0 2 4 6 8 10} is not in the catalog at any anchor.
        NamedScales.Match(new[] { 0, 2, 4, 6, 8, 10 }).Should().BeNull();
    }
}
