using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class PrincipleProgressionsTests
{
    private const int Ktet = 12;

    // Family data under the standard good-fraction defaults, as production builds it.
    private static IReadOnlyList<LcmFamily> Families() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);

    private static IReadOnlyList<PrincipleTarget> Compute(
        int[] chordKeys, int minSubsetNotes = 2, bool includeDim = false, bool rawBridges = false) =>
        PrincipleProgressions.Compute(chordKeys, Families(), minSubsetNotes, includeDim, rawBridges);

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

    // Reconstruct a placement's key set from a "{Lcm}@{At}" label.
    private static HashSet<int> KeysOf(string label)
    {
        var parts = label.Split('@');
        var family = Families().First(f => f.Lcm == int.Parse(parts[0]));
        return new HashSet<int>(Placements.Compute(family, int.Parse(parts[1]), Ktet).Keys);
    }

    // Independent power-of-2-preferred representative label for a given key set, recomputed over all
    // family placements (the oracle for the canonicalisation the production BuildCanonicalMap does).
    private static string CanonicalRep(HashSet<int> keys)
    {
        static bool Pow2(int n) => n > 0 && (n & (n - 1)) == 0;
        var rep = Families()
            .SelectMany(f => Placements.Sweep(f, Ktet))
            .Where(p => keys.SetEquals(p.Keys))
            .OrderBy(p => Pow2(p.Lcm) ? 0 : 1)
            .ThenBy(p => p.Lcm)
            .ThenBy(p => p.At)
            .First();
        return $"{rep.Lcm}@{rep.At}";
    }

    private static IEnumerable<string> AllLabels(PrincipleTarget t) =>
        t.SupersetBridges.Concat(t.SubsetBridges.SelectMany(triple => triple.Split('·')));

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

    // Category is derived from the raw placements, so it must be identical whether the displayed
    // bridges are the raw triples or the decluttered (isomorphism-collapsed + pooled) default.
    [Fact]
    public void Category_is_invariant_between_raw_and_pooled_bridges()
    {
        var pooled = Compute(new[] { 0, 4, 7 }, includeDim: true);
        var raw = Compute(new[] { 0, 4, 7 }, includeDim: true, rawBridges: true);

        foreach (var p in pooled)
        {
            var r = Target(raw, p.Keys.ToArray());
            p.Category.Should().Be(r.Category);
        }
    }

    // With the raw bridge lists the category is exactly the (superset-nonempty, subset-nonempty)
    // pair — the un-pooled lists carry every witness.
    [Fact]
    public void Raw_category_is_consistent_with_the_bridge_lists()
    {
        foreach (var t in Compute(new[] { 0, 4, 7 }, includeDim: true, rawBridges: true))
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

    // Every label in the decluttered output is the power-of-2-preferred representative of its key
    // set — so isomorphic aliases (e.g. 3@k for 4@k, or 9/10/12@k for 8@k) never appear.
    [Fact]
    public void Pooled_labels_are_canonical_power_of_two_representatives()
    {
        foreach (var t in Compute(new[] { 0, 4, 7 }, includeDim: true))
        {
            foreach (var label in AllLabels(t))
                label.Should().Be(CanonicalRep(KeysOf(label)));
        }
    }

    // Pooling keeps only maximal containing placements, so no superset bridge's key set is a proper
    // subset of another's.
    [Fact]
    public void Pooled_superset_bridges_form_an_antichain()
    {
        foreach (var t in Compute(new[] { 0, 4, 7 }, includeDim: true))
        {
            var keySets = t.SupersetBridges.Select(KeysOf).ToList();
            foreach (var x in keySets)
                keySets.Should().NotContain(y => y.Count > x.Count && x.IsSubsetOf(y));
        }
    }

    // Decluttering only ever removes labels — it never invents bridges the raw view lacked.
    [Fact]
    public void Pooling_never_grows_the_bridge_lists()
    {
        var pooled = Compute(new[] { 0, 4, 7 }, includeDim: true);
        var raw = Compute(new[] { 0, 4, 7 }, includeDim: true, rawBridges: true);

        foreach (var p in pooled)
        {
            var r = Target(raw, p.Keys.ToArray());
            p.SupersetBridges.Count.Should().BeLessThanOrEqualTo(r.SupersetBridges.Count);
            p.SubsetBridges.Count.Should().BeLessThanOrEqualTo(r.SubsetBridges.Count);
        }
    }

    // Doc example (composition-adjacency-and-preference.mdx): from C = {0 4 7}, Db = 4@1 = {1 5 8}
    // is reachable by the subset principle (via a shared subset) but the two chords share no single
    // containing placement — so subset, not superset.
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
