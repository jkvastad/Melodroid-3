using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class PrincipleProgressionsTests
{
    private const int Ktet = 12;

    // Family data under the standard good-fraction defaults, as production builds it.
    private static IReadOnlyList<LcmFamily> Families() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);

    private static IReadOnlyList<PrincipleTarget> Compute(int[] chordKeys, int minSubsetNotes = 2, bool includeDim = false) =>
        PrincipleProgressions.Compute(chordKeys, Families(), minSubsetNotes, includeDim);

    private static PrincipleTarget Target(IReadOnlyList<PrincipleTarget> targets, params int[] keys)
    {
        var want = keys.OrderBy(k => k).ToArray();
        return targets.Single(t => t.Keys.OrderBy(k => k).SequenceEqual(want));
    }

    // Independent oracle: the superset principle holds for A→B exactly when some good-LCM family
    // placement's key set contains the union A ∪ B. Reimplemented here from Placements.Sweep rather
    // than reusing the production supA-then-check-B path.
    private static bool SharesSuperset(int[] a, int[] b)
    {
        var union = new HashSet<int>(a.Concat(b));
        return Families()
            .SelectMany(f => Placements.Sweep(f, Ktet))
            .Any(p => union.IsSubsetOf(p.Keys));
    }

    [Fact]
    public void Default_yields_major_and_minor_targets_only()
    {
        var targets = Compute(new[] { 0, 4, 7 });
        targets.Should().HaveCount(24);
        targets.Should().OnlyContain(t => t.Quality == TriadQuality.Major || t.Quality == TriadQuality.Minor);
    }

    [Fact]
    public void IncludeDim_adds_the_twelve_diminished_targets()
    {
        var targets = Compute(new[] { 0, 4, 7 }, includeDim: true);
        targets.Should().HaveCount(36);
        targets.Count(t => t.Quality == TriadQuality.Diminished).Should().Be(12);
    }

    [Theory]
    [InlineData(0, 4, 7)] // major
    [InlineData(0, 3, 7)] // minor
    public void Superset_flag_matches_the_union_oracle(int k0, int k1, int k2)
    {
        var source = new[] { k0, k1, k2 };
        foreach (var target in Compute(source))
        {
            var hasSuperset = target.Category is ProgressionCategory.Superset or ProgressionCategory.Both;
            hasSuperset.Should().Be(SharesSuperset(source, target.Keys.ToArray()));
        }
    }

    [Fact]
    public void Category_is_consistent_with_the_bridge_lists()
    {
        foreach (var t in Compute(new[] { 0, 4, 7 }, includeDim: true))
        {
            var expected = (t.SupersetBridges.Count > 0, t.SubsetBridges.Count > 0) switch
            {
                (true, true) => ProgressionCategory.Both,
                (true, false) => ProgressionCategory.Superset,
                (false, true) => ProgressionCategory.Subset,
                _ => ProgressionCategory.Neither,
            };
            t.Category.Should().Be(expected);
        }
    }

    // Doc example (composition-adjacency-and-preference.mdx): from C = {0 4 7}, Db = 4@1 = {1 5 8}
    // is reachable by the subset principle (via shared subset 18@10) but the two chords share no
    // single containing placement — so subset, not superset.
    [Fact]
    public void C_to_Db_is_subset_only()
    {
        var db = Target(Compute(new[] { 0, 4, 7 }), 1, 5, 8);
        db.Category.Should().Be(ProgressionCategory.Subset);
        db.SupersetBridges.Should().BeEmpty();
        db.SubsetBridges.Should().NotBeEmpty();
    }

    // Doc example: from C, Eb = 4@3 = {3 7 10} is reachable by the superset principle (both under
    // 15@7). With any-family subset bridges it also picks up subset bridges, so the category is Both;
    // the salient assertion is that the superset bridge is present.
    [Fact]
    public void C_to_Eb_shares_a_superset()
    {
        var eb = Target(Compute(new[] { 0, 4, 7 }), 3, 7, 10);
        eb.SupersetBridges.Should().NotBeEmpty();
        eb.Category.Should().BeOneOf(ProgressionCategory.Superset, ProgressionCategory.Both);
    }

    // Raising the minimum subset size can only ever remove subset bridges, never add them: the
    // candidate S families at a higher threshold are a subset of those at a lower one.
    [Fact]
    public void Raising_min_subset_notes_never_adds_subset_bridges()
    {
        var source = new[] { 0, 4, 7 };
        var loose = Compute(source, minSubsetNotes: 2);
        var tight = Compute(source, minSubsetNotes: 4);

        foreach (var t in tight)
        {
            var looseBridges = new HashSet<string>(Target(loose, t.Keys.ToArray()).SubsetBridges);
            t.SubsetBridges.Should().OnlyContain(b => looseBridges.Contains(b));
        }
    }
}
