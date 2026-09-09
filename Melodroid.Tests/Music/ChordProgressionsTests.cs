using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class ChordProgressionsTests
{
    // The family data under the standard good-fraction defaults, as production builds it.
    private static (IReadOnlyList<LcmFamily> Families, IReadOnlyList<FamilyRelation> Relations, LcmFamily Lcm24, LcmFamily Lcm15) Data()
    {
        var families = LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);
        var relations = FamilyRelations.Compute(families);
        return (families, relations, families.First(f => f.Lcm == 24), families.First(f => f.Lcm == 15));
    }

    private static IReadOnlyList<ProgressionTarget> Compute(int[] chordKeys, bool stable)
    {
        var (families, relations, lcm24, lcm15) = Data();
        return ChordProgressions.Compute(chordKeys, stable, families, relations, lcm24, lcm15);
    }

    // Roots of the reachable targets of a given quality, sorted — the independent oracle for the
    // reachability tests below.
    private static int[] ReachableRoots(int[] chordKeys, bool stable, TriadQuality quality) =>
        Compute(chordKeys, stable)
            .Where(t => t.Quality == quality)
            .Select(t => t.Root)
            .OrderBy(r => r)
            .ToArray();

    // --- Stable walk: pins the derived per-quality bridge labels (formerly hardcoded). ---

    // Equivalence oracle: the derived stable melodic supersets for the three tabled triads must
    // reproduce the documented per-quality superset bridge labels.
    [Theory]
    [InlineData(new[] { 0, 4, 7 }, new[] { "24@0", "24@5", "24@7", "15s@7" })]   // major
    [InlineData(new[] { 0, 3, 7 }, new[] { "24@3", "24@8", "24@10", "15s@2" })]  // minor
    [InlineData(new[] { 0, 3, 6 }, new[] { "24@1", "15s@3" })]                   // dim
    public void Stable_superset_bridges_match_the_documented_tables(int[] chordKeys, string[] expected)
    {
        var targets = Compute(chordKeys, stable: true);

        var labels = targets.SelectMany(t => t.SupersetBridges).Distinct();
        labels.Should().BeEquivalentTo(expected);
    }

    // Independent oracle for the stable adjacency rule (15s@X ⇒ 24@(X+1), 24@(X+8) mod 12): the
    // distinct adjacency bridge labels for each tabled triad must be exactly the derived placements.
    [Theory]
    [InlineData(new[] { 0, 4, 7 }, new[] { "24@8", "24@3" })]        // 15s@7 ⇒ 24@8, 24@3 (15→3)
    [InlineData(new[] { 0, 3, 7 }, new[] { "24@3", "24@10" })]       // 15s@2 ⇒ 24@3, 24@10
    [InlineData(new[] { 0, 3, 6 }, new[] { "24@4", "24@11" })]       // 15s@3 ⇒ 24@4, 24@11
    public void Stable_adjacency_bridges_match_the_documented_derivation(int[] chordKeys, string[] expected)
    {
        var targets = Compute(chordKeys, stable: true);

        var labels = targets.SelectMany(t => t.AdjacencyBridges).Distinct();
        labels.Should().BeEquivalentTo(expected);
    }

    // --- Strict walk (default): reproduces the four progression tables in the composition doc. ---

    // Major source reaches every major triad but the tritone 4@6.
    [Fact]
    public void Strict_major_to_major_reaches_all_but_the_tritone()
    {
        ReachableRoots(new[] { 0, 4, 7 }, stable: false, TriadQuality.Major)
            .Should().Equal(0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11);
    }

    // Minor source reaches only the minors at {0, ±2, ±5}: relative fifths + whole-step neighbours.
    [Fact]
    public void Strict_minor_to_minor_reaches_only_fifths_and_whole_step_neighbours()
    {
        ReachableRoots(new[] { 0, 3, 7 }, stable: false, TriadQuality.Minor)
            .Should().Equal(0, 2, 5, 7, 10);
    }

    // Major source reaches eight minors: the major scale's own roots plus min@10.
    [Fact]
    public void Strict_major_to_minor_reaches_eight_minors()
    {
        ReachableRoots(new[] { 0, 4, 7 }, stable: false, TriadQuality.Minor)
            .Should().Equal(0, 2, 4, 5, 7, 9, 10, 11);
    }

    // Minor source reaches eight majors, including the parallel major 4@0 via the 15@7 superset.
    [Fact]
    public void Strict_minor_to_major_reaches_eight_majors()
    {
        ReachableRoots(new[] { 0, 3, 7 }, stable: false, TriadQuality.Major)
            .Should().Equal(0, 1, 2, 3, 5, 7, 8, 10);
    }

    // The strict walk reaches at least everything the stable walk does (bidirectional adjacency only
    // adds placements). Verified on the major source, where the distant majors 4@4,9,11 appear.
    [Fact]
    public void Strict_is_a_superset_of_stable()
    {
        string Sig(ProgressionTarget t) => $"{t.Quality}:{t.Root}";

        var strict = Compute(new[] { 0, 4, 7 }, stable: false).Select(Sig).ToHashSet();
        var stable = Compute(new[] { 0, 4, 7 }, stable: true).Select(Sig);

        strict.Should().Contain(stable);
        strict.Should().Contain(new[] { "Major:4", "Major:9", "Major:11" });
    }

    [Fact]
    public void A_chord_can_progress_to_itself_via_its_own_supersets()
    {
        var targets = Compute(new[] { 0, 4, 7 }, stable: true);

        targets.Should().Contain(t =>
            t.Quality == TriadQuality.Major && t.Root == 0 && t.SupersetBridges.Count > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Every_returned_target_carries_a_bridge(bool stable)
    {
        var targets = Compute(new[] { 0, 3, 7 }, stable);

        targets.Should().OnlyContain(t => t.SupersetBridges.Count > 0 || t.AdjacencyBridges.Count > 0);
        targets.Should().NotBeEmpty();
    }

    // A non-triad source (0 4 7 10) yields progressions instead of erroring; every target still
    // carries a bridge.
    [Fact]
    public void A_non_triad_source_yields_bridge_carrying_targets()
    {
        var targets = Compute(new[] { 0, 4, 7, 10 }, stable: false);

        targets.Should().NotBeEmpty();
        targets.Should().OnlyContain(t => t.SupersetBridges.Count > 0 || t.AdjacencyBridges.Count > 0);
    }
}
