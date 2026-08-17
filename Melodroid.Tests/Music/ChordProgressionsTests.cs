using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class ChordProgressionsTests
{
    // The family data under the standard good-fraction defaults, as production builds it.
    private static (IReadOnlyList<LcmFamily> Families, IReadOnlyList<FamilyRelation> Relations, LcmFamily Lcm24) Data()
    {
        var families = LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);
        var relations = FamilyRelations.Compute(families);
        return (families, relations, families.First(f => f.Lcm == 24));
    }

    private static IReadOnlyList<ProgressionTarget> Compute(int[] chordKeys, bool supersets, bool adjacency)
    {
        var (families, relations, lcm24) = Data();
        return ChordProgressions.Compute(chordKeys, supersets, adjacency, families, relations, lcm24);
    }

    // Equivalence oracle: the derived stable melodic supersets for the three tabled triads must
    // reproduce the documented per-quality bridge labels (formerly hardcoded). This pins the
    // generalization back to the "Perception Based Progression" tables in the voicings doc.
    [Theory]
    [InlineData(new[] { 0, 4, 7 }, new[] { "24@0", "24@5", "24@7", "15s@7" })]   // major
    [InlineData(new[] { 0, 3, 7 }, new[] { "24@3", "24@8", "24@10", "15s@2" })]  // minor
    [InlineData(new[] { 0, 3, 6 }, new[] { "24@1", "15s@3" })]                   // dim
    public void Superset_bridges_match_the_documented_tables(int[] chordKeys, string[] expected)
    {
        var targets = Compute(chordKeys, supersets: true, adjacency: false);

        var labels = targets.SelectMany(t => t.SupersetBridges).Distinct();
        labels.Should().BeEquivalentTo(expected);
    }

    // Independent oracle for the adjacency rule (15s@X ⇒ 24@(X+1), 24@(X+8) mod 12): the distinct
    // adjacency bridge labels for each tabled triad must be exactly the derived placements.
    [Theory]
    [InlineData(new[] { 0, 4, 7 }, new[] { "24@8", "24@3" })]        // 15s@7 ⇒ 24@8, 24@3 (15→3)
    [InlineData(new[] { 0, 3, 7 }, new[] { "24@3", "24@10" })]       // 15s@2 ⇒ 24@3, 24@10
    [InlineData(new[] { 0, 3, 6 }, new[] { "24@4", "24@11" })]       // 15s@3 ⇒ 24@4, 24@11
    public void Adjacency_bridges_match_the_documented_derivation(int[] chordKeys, string[] expected)
    {
        var targets = Compute(chordKeys, supersets: false, adjacency: true);

        var labels = targets.SelectMany(t => t.AdjacencyBridges).Distinct();
        labels.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Both_is_the_union_of_supersets_only_and_adjacency_only()
    {
        string Sig(ProgressionTarget t) => string.Join(",", t.Keys);

        var supersets = Compute(new[] { 0, 4, 7 }, true, false).Select(Sig);
        var adjacency = Compute(new[] { 0, 4, 7 }, false, true).Select(Sig);
        var both = Compute(new[] { 0, 4, 7 }, true, true).Select(Sig);

        both.Should().BeEquivalentTo(supersets.Union(adjacency));
    }

    [Fact]
    public void A_chord_can_progress_to_itself_via_its_own_supersets()
    {
        var targets = Compute(new[] { 0, 4, 7 }, true, false);

        targets.Should().Contain(t =>
            t.Quality == TriadQuality.Major && t.Root == 0 && t.SupersetBridges.Count > 0);
    }

    [Fact]
    public void Every_returned_target_carries_a_bridge_under_an_included_rule()
    {
        var targets = Compute(new[] { 0, 3, 7 }, true, true);

        targets.Should().OnlyContain(t => t.SupersetBridges.Count > 0 || t.AdjacencyBridges.Count > 0);
        targets.Should().NotBeEmpty();
    }

    // The generalization's point: a non-triad source (0 4 7 10) now yields progressions instead of
    // erroring. Every target still carries a bridge under an included rule.
    [Fact]
    public void A_non_triad_source_yields_bridge_carrying_targets()
    {
        var targets = Compute(new[] { 0, 4, 7, 10 }, true, true);

        targets.Should().NotBeEmpty();
        targets.Should().OnlyContain(t => t.SupersetBridges.Count > 0 || t.AdjacencyBridges.Count > 0);
    }
}
