using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class ChordProgressionsTests
{
    // The lcm-24 family under the standard good-fraction defaults, as production builds it.
    private static LcmFamily Lcm24Family()
    {
        var families = LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);
        return families.First(f => f.Lcm == 24);
    }

    [Theory]
    [InlineData(new[] { 0, 4, 7 }, TriadQuality.Major, 0)]
    [InlineData(new[] { 7, 4, 0 }, TriadQuality.Major, 0)]   // order-independent
    [InlineData(new[] { 2, 6, 9 }, TriadQuality.Major, 2)]
    [InlineData(new[] { 0, 3, 7 }, TriadQuality.Minor, 0)]
    [InlineData(new[] { 4, 7, 11 }, TriadQuality.Minor, 4)]
    [InlineData(new[] { 0, 3, 6 }, TriadQuality.Diminished, 0)]
    [InlineData(new[] { 11, 2, 5 }, TriadQuality.Diminished, 11)]
    public void Identify_matches_quality_and_root(int[] keys, TriadQuality quality, int root)
    {
        ChordProgressions.Identify(keys).Should().Be((quality, root));
    }

    [Theory]
    [InlineData(new[] { 0, 1, 2 })]   // cluster, not a triad
    [InlineData(new[] { 0, 4 })]      // dyad
    [InlineData(new[] { 0, 4, 8 })]   // augmented — not tabled
    public void Identify_returns_null_for_non_tabled_sets(int[] keys)
    {
        ChordProgressions.Identify(keys).Should().BeNull();
    }

    // Independent oracle for the adjacency rule (15s@X ⇒ 24@(X+1), 24@(X+8) mod 12): the distinct
    // adjacency bridge labels for each quality at root 0 must be exactly the derived placements.
    [Theory]
    [InlineData(TriadQuality.Major, new[] { "24@8", "24@3" })]        // 15s@7 ⇒ 24@8, 24@3 (15→3)
    [InlineData(TriadQuality.Minor, new[] { "24@3", "24@10" })]       // 15s@2 ⇒ 24@3, 24@10
    [InlineData(TriadQuality.Diminished, new[] { "24@4", "24@11" })]  // 15s@3 ⇒ 24@4, 24@11
    public void Adjacency_bridges_match_the_documented_derivation(TriadQuality quality, string[] expected)
    {
        var targets = ChordProgressions.Compute(quality, 0, includeSupersets: false, includeAdjacency: true, Lcm24Family());

        var labels = targets.SelectMany(t => t.AdjacencyBridges).Distinct();
        labels.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Both_is_the_union_of_supersets_only_and_adjacency_only()
    {
        var family = Lcm24Family();
        string Sig(ProgressionTarget t) => string.Join(",", t.Keys);

        var supersets = ChordProgressions.Compute(TriadQuality.Major, 0, true, false, family).Select(Sig);
        var adjacency = ChordProgressions.Compute(TriadQuality.Major, 0, false, true, family).Select(Sig);
        var both = ChordProgressions.Compute(TriadQuality.Major, 0, true, true, family).Select(Sig);

        both.Should().BeEquivalentTo(supersets.Union(adjacency));
    }

    [Fact]
    public void A_chord_can_progress_to_itself_via_its_own_supersets()
    {
        var targets = ChordProgressions.Compute(TriadQuality.Major, 0, true, false, Lcm24Family());

        targets.Should().Contain(t =>
            t.Quality == TriadQuality.Major && t.Root == 0 && t.SupersetBridges.Count > 0);
    }

    [Fact]
    public void Every_returned_target_carries_a_bridge_under_an_included_rule()
    {
        var targets = ChordProgressions.Compute(TriadQuality.Minor, 0, true, true, Lcm24Family());

        targets.Should().OnlyContain(t => t.SupersetBridges.Count > 0 || t.AdjacencyBridges.Count > 0);
        targets.Should().NotBeEmpty();
    }
}
