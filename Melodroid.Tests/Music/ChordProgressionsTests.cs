using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class ChordProgressionsTests
{
    private const int Ktet = 12;

    // Family data under the standard good-fraction defaults, as production builds it.
    private static IReadOnlyList<LcmFamily> Families() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);

    private static IReadOnlyList<ProgressionTarget> Compute(
        int[] chordKeys, int minSubsetNotes = 2, bool includeDim = false,
        bool collapseIsomorphic = false, bool poolMaximal = false,
        CollapseFilter dropCollapsed = CollapseFilter.None) =>
        ChordProgressions.Compute(chordKeys, Families(), minSubsetNotes, includeDim,
            collapseIsomorphic, poolMaximal, dropCollapsed);

    private static (ProgressionCategory Category, IReadOnlyList<ProgressionTriplet> Triplets) ComputePair(
        int[] origin, int[] target, int minSubsetNotes = 2,
        bool collapseIsomorphic = false, bool poolMaximal = false,
        CollapseFilter dropCollapsed = CollapseFilter.None) =>
        ChordProgressions.ComputePair(origin, target, Families(), minSubsetNotes,
            collapseIsomorphic, poolMaximal, dropCollapsed);

    // Parse a raw "{Lcm}@{At}" label into its (lcm, at) pair. Independent of production's label code.
    private static (int Lcm, int At) ParseLabel(string label)
    {
        var parts = label.Split('@');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    // Is `label` an lcm-15 placement collapsed by `chord` — i.e. its collapsing key (At+8) mod 12 is
    // one of the chord keys? Reimplements the (At+8) mod 12 rule as the independent oracle.
    private static bool Collapsed15(string label, IEnumerable<int> chord)
    {
        var (lcm, at) = ParseLabel(label);
        return lcm == 15 && chord.Contains((at + 8) % 12);
    }

    // Is `label` an lcm-20 placement collapsed by `chord` — i.e. its collapsing key (At+3) mod 12 is
    // one of the chord keys? Reimplements the (At+3) mod 12 rule as the independent oracle.
    private static bool Collapsed20(string label, IEnumerable<int> chord)
    {
        var (lcm, at) = ParseLabel(label);
        return lcm == 20 && chord.Contains((at + 3) % 12);
    }

    private static ProgressionTarget Target(IReadOnlyList<ProgressionTarget> targets, params int[] keys)
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

    private static IEnumerable<string> AllLabels(ProgressionTarget t) =>
        t.SupersetBridges.Concat(t.SubsetBridges.SelectMany(triple => triple.Split('·')));

    // BridgeLabels(triplets) must reproduce exactly the SupersetBridges / SubsetBridges strings the
    // convenience form (Compute) attaches to the same target — the refactor that extracted the inline
    // label logic out of Compute is behaviour-preserving. C → F major (both superset and subset paths).
    [Fact]
    public void BridgeLabels_matches_convenience_form_bridges_for_the_same_pair()
    {
        int[] origin = { 0, 4, 7 };
        int[] target = { 0, 5, 9 };   // F major triad, root 5

        var fromConvenience = Target(Compute(origin), target);
        var (_, triplets) = ComputePair(origin, target);
        var (superset, subset) = ChordProgressions.BridgeLabels(triplets);

        superset.Should().Equal(fromConvenience.SupersetBridges);
        subset.Should().Equal(fromConvenience.SubsetBridges);
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

    // A single-placement progression (SupersetBridges non-empty) exists for A→B exactly when some
    // placement contains the union A ∪ B — the independent union oracle.
    [Theory]
    [InlineData(0, 4, 7)] // major
    [InlineData(0, 3, 7)] // minor
    public void Single_placement_progression_matches_the_union_oracle(int k0, int k1, int k2)
    {
        var source = new[] { k0, k1, k2 };
        foreach (var target in Compute(source))
        {
            var holdsBoth = target.SupersetBridges.Count > 0;
            holdsBoth.Should().Be(SharesSuperset(source, target.Keys.ToArray()));
        }
    }

    // Reach is derived from the raw placements, so it must be identical whether the displayed bridges
    // are the raw triples or the decluttered (isomorphism-collapsed + pooled) default.
    [Fact]
    public void Reach_is_invariant_between_raw_and_pooled_bridges()
    {
        var pooled = Compute(new[] { 0, 4, 7 }, includeDim: true, collapseIsomorphic: true, poolMaximal: true);
        var raw = Compute(new[] { 0, 4, 7 }, includeDim: true);

        foreach (var p in pooled)
        {
            var r = Target(raw, p.Keys.ToArray());
            p.Reach.Should().Be(r.Reach);
        }
    }

    // Reach is exactly: Placement when a placement progression exists (either bridge list non-empty),
    // else Composite when a composite scale hosts the move (CompositeBridges non-empty), else
    // Unreachable. CompositeBridges are populated only in the Composite tier.
    [Fact]
    public void Raw_reach_is_consistent_with_the_bridge_lists()
    {
        foreach (var t in Compute(new[] { 0, 4, 7 }, includeDim: true))
        {
            var hasPlacement = t.SupersetBridges.Count > 0 || t.SubsetBridges.Count > 0;
            var expected = hasPlacement
                ? ProgressionReach.Placement
                : t.CompositeBridges.Count > 0 ? ProgressionReach.Composite : ProgressionReach.Unreachable;
            t.Reach.Should().Be(expected);

            if (hasPlacement) t.CompositeBridges.Should().BeEmpty();
        }
    }

    // Every label in the decluttered output is the power-of-2-preferred representative of its key
    // set — so isomorphic aliases (e.g. 3@k for 4@k, or 9/10/12@k for 8@k) never appear.
    [Fact]
    public void Pooled_labels_are_canonical_power_of_two_representatives()
    {
        foreach (var t in Compute(new[] { 0, 4, 7 }, includeDim: true, collapseIsomorphic: true, poolMaximal: true))
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
        foreach (var t in Compute(new[] { 0, 4, 7 }, includeDim: true, collapseIsomorphic: true, poolMaximal: true))
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
        var pooled = Compute(new[] { 0, 4, 7 }, includeDim: true, collapseIsomorphic: true, poolMaximal: true);
        var raw = Compute(new[] { 0, 4, 7 }, includeDim: true);

        foreach (var p in pooled)
        {
            var r = Target(raw, p.Keys.ToArray());
            p.SupersetBridges.Count.Should().BeLessThanOrEqualTo(r.SupersetBridges.Count);
            p.SubsetBridges.Count.Should().BeLessThanOrEqualTo(r.SubsetBridges.Count);
        }
    }

    // Doc example (progression-principles.mdx): from C = {0 4 7}, Db = 4@1 = {1 5 8} is reachable by a
    // bridged placement progression (via a shared subset) but the two chords share no single containing
    // placement — so bridged only, no single-placement bridge. Either way the reach is Placement.
    [Fact]
    public void C_to_Db_is_bridged_only()
    {
        var db = Target(Compute(new[] { 0, 4, 7 }), 1, 5, 8);
        db.Reach.Should().Be(ProgressionReach.Placement);
        db.SupersetBridges.Should().BeEmpty();
        db.SubsetBridges.Should().NotBeEmpty();
    }

    // Doc example: from C, Eb = 4@3 = {3 7 10} is held by a single placement (both under 15@7), so a
    // single-placement progression exists and the reach is Placement.
    [Fact]
    public void C_to_Eb_has_a_single_placement_progression()
    {
        var eb = Target(Compute(new[] { 0, 4, 7 }), 3, 7, 10);
        eb.SupersetBridges.Should().NotBeEmpty();
        eb.Reach.Should().Be(ProgressionReach.Placement);
    }

    // --drop-collapsed defaults to None, so an explicit None must reproduce the default behaviour
    // exactly (categories and bridge lists alike).
    [Fact]
    public void DropCollapsed_none_matches_the_default()
    {
        var @default = Compute(new[] { 0, 4, 7 }, includeDim: true);
        var none = Compute(new[] { 0, 4, 7 }, includeDim: true, dropCollapsed: CollapseFilter.None);

        foreach (var t in none)
        {
            var d = Target(@default, t.Keys.ToArray());
            t.Reach.Should().Be(d.Reach);
            t.SupersetBridges.Should().Equal(d.SupersetBridges);
            t.SubsetBridges.Should().Equal(d.SubsetBridges);
            t.CompositeBridges.Should().Equal(d.CompositeBridges);
        }
    }

    // Origin-side superset bridges come straight from the source chord's containing placements. With
    // the default (None) the C-major self-target {0 4 7} is bridged by collapsed lcm-15 placements
    // (15@4, collapsing key 0; 15@11, collapsing key 7 — both ∈ {0 4 7}); --drop-collapsed origin
    // disqualifies them, and no collapsed lcm-15 may survive as a superset bridge for any target.
    [Fact]
    public void DropCollapsed_origin_removes_collapsed_fifteen_superset_bridges()
    {
        var source = new[] { 0, 4, 7 };

        var self = Target(Compute(source), 0, 4, 7);
        self.SupersetBridges.Should().Contain("15@4").And.Contain("15@11");

        var origin = Compute(source, includeDim: true, dropCollapsed: CollapseFilter.Origin);
        foreach (var t in origin)
            t.SupersetBridges.Should().OnlyContain(b => !Collapsed15(b, source));
    }

    // A superset bridge is played as A ∪ B, so --drop-collapsed target (and both) must also remove
    // lcm-15 placements collapsed by the *target* triad. From C, the C-minor target {0 3 7} sits
    // under 15@7 (collapsing key 3 ∈ {0 3 7}); with --target it must disappear as a superset bridge,
    // and no collapsed lcm-15 may survive as a superset bridge for any target.
    [Fact]
    public void DropCollapsed_target_removes_collapsed_fifteen_superset_bridges()
    {
        var source = new[] { 0, 4, 7 };

        var none = Target(Compute(source), 0, 3, 7);
        none.SupersetBridges.Should().Contain("15@7");

        foreach (var filter in new[] { CollapseFilter.Target, CollapseFilter.Both })
        {
            var results = Compute(source, includeDim: true, dropCollapsed: filter);
            Target(results, 0, 3, 7).SupersetBridges.Should().NotContain("15@7");
            foreach (var t in results)
                t.SupersetBridges.Should().OnlyContain(b => !Collapsed15(b, t.Keys));
        }
    }

    // In a raw P_A·S·P_B subset triple, P_A ⊇ source (origin side) and P_B ⊇ target (target side).
    // --drop-collapsed origin removes collapsed lcm-15 placements from the P_A slot; --target removes
    // them from the P_B slot.
    [Fact]
    public void DropCollapsed_filters_the_respective_subset_triple_slot()
    {
        var source = new[] { 0, 4, 7 };

        var origin = Compute(source, includeDim: true, dropCollapsed: CollapseFilter.Origin);
        foreach (var t in origin)
            foreach (var triple in t.SubsetBridges)
                Collapsed15(triple.Split('·')[0], source).Should().BeFalse();

        var target = Compute(source, includeDim: true, dropCollapsed: CollapseFilter.Target);
        foreach (var t in target)
            foreach (var triple in t.SubsetBridges)
                Collapsed15(triple.Split('·')[2], t.Keys).Should().BeFalse();
    }

    // lcm-20 is unstable too: 20@At collapses to 15@(At+7) on key (At+3) mod 12. With the default the
    // C-major self-target {0 4 7} is bridged by 20@4 (collapsing key 7 ∈ {0 4 7}); --drop-collapsed
    // origin disqualifies it, and no collapsed lcm-20 may survive as a superset bridge for any target.
    [Fact]
    public void DropCollapsed_origin_removes_collapsed_twenty_superset_bridges()
    {
        var source = new[] { 0, 4, 7 };

        var self = Target(Compute(source), 0, 4, 7);
        self.SupersetBridges.Should().Contain("20@4");

        var origin = Compute(source, includeDim: true, dropCollapsed: CollapseFilter.Origin);
        foreach (var t in origin)
            t.SupersetBridges.Should().OnlyContain(b => !Collapsed20(b, source));
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

    // Standard form, doc example: C = {0 4 7} → Eb = {3 7 10} has a superset triplet (both chords
    // sit under 15@7). A superset triplet's three slots are the one containing placement (P, P, P).
    [Fact]
    public void ComputePair_C_to_Eb_has_a_superset_triplet_via_fifteen_at_seven()
    {
        var (category, triplets) = ComputePair(new[] { 0, 4, 7 }, new[] { 3, 7, 10 });

        category.Should().BeOneOf(ProgressionCategory.Superset, ProgressionCategory.Both);
        triplets.Should().Contain(t =>
            t.Principle == ProgressionPrinciple.Superset && t.Bridge.Lcm == 15 && t.Bridge.At == 7);

        foreach (var t in triplets.Where(t => t.Principle == ProgressionPrinciple.Superset))
        {
            t.Origin.Should().Be(t.Bridge);
            t.Target.Should().Be(t.Bridge);
        }
    }

    // Standard form, doc example: C → Db = {1 5 8} is subset-only — no single placement holds both,
    // so every triplet is a subset triple whose origin slot ⊇ C and target slot ⊇ Db.
    [Fact]
    public void ComputePair_C_to_Db_is_subset_only_with_containing_slots()
    {
        var origin = new[] { 0, 4, 7 };
        var target = new[] { 1, 5, 8 };
        var (category, triplets) = ComputePair(origin, target);

        category.Should().Be(ProgressionCategory.Subset);
        triplets.Should().NotBeEmpty();
        foreach (var t in triplets)
        {
            t.Principle.Should().Be(ProgressionPrinciple.Subset);
            new HashSet<int>(t.Origin.Keys).IsSupersetOf(origin).Should().BeTrue();
            new HashSet<int>(t.Target.Keys).IsSupersetOf(target).Should().BeTrue();
        }
    }

    // ComputePair is the per-pair core of the convenience enumeration: its placement-progression
    // category maps to the target's reachability tier — a non-Neither category is a Placement reach;
    // a Neither category escalates to Composite (a host exists) or Unreachable. The raw triplet list is
    // non-empty exactly when a placement progression exists.
    [Fact]
    public void ComputePair_agrees_with_Compute_per_target()
    {
        var origin = new[] { 0, 4, 7 };
        foreach (var t in Compute(origin, includeDim: true))
        {
            var (category, triplets) = ComputePair(origin, t.Keys.ToArray());
            var expectedReach = category == ProgressionCategory.Neither
                ? (t.CompositeBridges.Count > 0 ? ProgressionReach.Composite : ProgressionReach.Unreachable)
                : ProgressionReach.Placement;
            t.Reach.Should().Be(expectedReach);
            (triplets.Count == 0).Should().Be(category == ProgressionCategory.Neither);
        }
    }

    // When a placement progression exists, ComputePairReach reports Placement with its triplets and no
    // composite hosts (composites are the no-placement-progression fallback). C → F at the default
    // minimum is placement-reachable.
    [Fact]
    public void ComputePairReach_placement_tier_has_triplets_and_no_composites()
    {
        var (reach, triplets, composites) = ChordProgressions.ComputePairReach(
            new[] { 0, 4, 7 }, new[] { 0, 5, 9 }, Families(), minSubsetNotes: 2);

        reach.Should().Be(ProgressionReach.Placement);
        triplets.Should().NotBeEmpty();
        composites.Should().BeEmpty();
    }

    // Starving placement progressions (a large shared-bridge floor) escalates a reachable move to a
    // composite placement progression: C → C#m = {1 4 8} has no size-6 bridged placement progression,
    // so it is hosted by composite scales (harmonic minor at anchor 5 among them). Independent oracle:
    // no placement progression exists (ComputePair is Neither), yet a named scale hosts the move.
    [Fact]
    public void ComputePairReach_escalates_to_composite_hosts_when_no_placement_progression()
    {
        var origin = new[] { 0, 4, 7 };
        var target = new[] { 1, 4, 8 };

        var (category, _) = ComputePair(origin, target, minSubsetNotes: 6);
        category.Should().Be(ProgressionCategory.Neither);
        NamedScales.ContainingChord(origin.Concat(target).Distinct().ToArray(), Ktet)
            .Should().NotBeEmpty(); // a scale hosts the move → Composite, not Unreachable

        var (reach, triplets, composites) = ChordProgressions.ComputePairReach(
            origin, target, Families(), minSubsetNotes: 6);

        reach.Should().Be(ProgressionReach.Composite);
        triplets.Should().BeEmpty();
        composites.Should().NotBeEmpty();
        composites.Should().OnlyContain(c => c.StartsWith("↑"));
        composites.Should().Contain("↑hm@5"); // the harmonic-minor host from the doc's C→minor discussion
    }

    private static Placement Place(int lcm, int at) =>
        Placements.Compute(Families().Single(f => f.Lcm == lcm), at, Ktet);

    // Independent oracle: do distinct placements a and b share a family placement S of ≥ min keys that
    // is a subset of both? Reimplemented from Placements.Sweep rather than the production SubsetTriplets.
    private static bool ShareSubsetOfSize(IReadOnlyCollection<int> a, IReadOnlyCollection<int> b, int min)
    {
        var inter = new HashSet<int>(a);
        inter.IntersectWith(b);
        if (inter.Count < min) return false;
        return Families()
            .SelectMany(f => Placements.Sweep(f, Ktet))
            .Any(s => s.Keys.Count >= min && s.Keys.All(inter.Contains));
    }

    // SubsetTripletsBetween classifies two explicit placement lists by the subset principle; its
    // existence must match the independent shared-subset oracle for each pair, and every emitted
    // triplet is a subset triple.
    [Theory]
    [InlineData(15, 11, 24, 0)]   // harmonic-minor composite neighbours
    [InlineData(24, 0, 24, 7)]    // two lcm-24 placements a fifth apart
    [InlineData(4, 0, 4, 6)]      // tritone-apart major triads (empty intersection)
    public void SubsetTripletsBetween_ExistenceMatchesSharedSubsetOracle(
        int lcmA, int atA, int lcmB, int atB)
    {
        var a = new[] { Place(lcmA, atA) };
        var b = new[] { Place(lcmB, atB) };

        var triplets = ChordProgressions.SubsetTripletsBetween(a, b, Families(), minSubsetNotes: 4);

        (triplets.Count > 0).Should().Be(ShareSubsetOfSize(a[0].Keys, b[0].Keys, 4));
        triplets.Should().OnlyContain(t => t.Principle == ProgressionPrinciple.Subset);
    }

    [Fact]
    public void SubsetTripletsBetween_RecoversAKnownCompositePair_WithCorrectSlots()
    {
        // 24@0 and 24@5 are the bebop-dominant composite (they share 18@7), so the between-wrapper must
        // find the triple with those exact P_A / P_B slots.
        var a = new[] { Place(24, 0) };
        var b = new[] { Place(24, 5) };

        var triplets = ChordProgressions.SubsetTripletsBetween(a, b, Families(), minSubsetNotes: 4);

        triplets.Should().NotBeEmpty();
        triplets.Should().Contain(t =>
            t.Origin.Lcm == 24 && t.Origin.At == 0 && t.Target.Lcm == 24 && t.Target.At == 5);
    }

    [Fact]
    public void SubsetTripletsBetween_SamePlacementBothSides_IsEmpty()
    {
        // Distinctness: a placement cannot subset-bridge to itself (that is the superset principle).
        var p = new[] { Place(15, 11) };
        ChordProgressions.SubsetTripletsBetween(p, p, Families(), minSubsetNotes: 4).Should().BeEmpty();
    }
}
