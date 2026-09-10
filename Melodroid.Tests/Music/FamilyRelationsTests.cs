using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class FamilyRelationsTests
{
    [Fact]
    public void Lcm3_and_Lcm4_are_isomorphic_via_three_halves_base()
    {
        var families = DefaultFamilies();

        var relations = FamilyRelations.Compute(families);

        var iso = relations.Single(r =>
            r.Kind == RelationKind.Isomorphism &&
            ((r.FromLcm == 3 && r.ToLcm == 4) || (r.FromLcm == 4 && r.ToLcm == 3)));
        iso.Base.Should().NotBeNull();
        // Either (3,4) with base 4/3, or (4,3) with base 3/2 â€” both valid; we canonicalize From<To.
        iso.FromLcm.Should().Be(3);
        iso.ToLcm.Should().Be(4);
        iso.Base!.Value.Should().Be(new Fraction(4, 3));
    }

    [Fact]
    public void Lcm18_is_renormalized_subset_of_Lcm24_via_four_thirds_base()
    {
        var families = DefaultFamilies();

        var relations = FamilyRelations.Compute(families);

        var edge = relations.Single(r =>
            r.Kind == RelationKind.RenormalizedSubset && r.FromLcm == 18 && r.ToLcm == 24);
        edge.Base.Should().Be(new Fraction(4, 3));
    }

    [Fact]
    public void Lcm4_is_literal_subset_of_Lcm8()
    {
        var families = DefaultFamilies();

        var relations = FamilyRelations.Compute(families);

        relations.Should().Contain(r =>
            r.Kind == RelationKind.LiteralSubset && r.FromLcm == 4 && r.ToLcm == 8);
    }

    [Fact]
    public void Hasse_reduction_drops_transitive_literal_subset_edges()
    {
        // 1 | 2 | 4, so Lcm1 -> Lcm4 (literal) is implied via Lcm2; should not appear.
        var families = DefaultFamilies();

        var relations = FamilyRelations.Compute(families);

        relations.Should().NotContain(r =>
            r.Kind == RelationKind.LiteralSubset && r.FromLcm == 1 && r.ToLcm == 4);
        relations.Should().NotContain(r =>
            r.Kind == RelationKind.LiteralSubset && r.FromLcm == 1 && r.ToLcm == 24);
        // But Lcm1 -> Lcm2 should remain (direct).
        relations.Should().Contain(r =>
            r.Kind == RelationKind.LiteralSubset && r.FromLcm == 1 && r.ToLcm == 2);
    }

    [Fact]
    public void No_self_edges_emitted()
    {
        var families = DefaultFamilies();

        var relations = FamilyRelations.Compute(families);

        relations.Should().NotContain(r => r.FromLcm == r.ToLcm);
    }

    [Fact]
    public void Isomorphism_edges_are_emitted_in_canonical_direction_only()
    {
        // Iso is symmetric. We emit only one direction per unordered pair (smaller Lcm to larger).
        var families = DefaultFamilies();

        var relations = FamilyRelations.Compute(families);

        var isoEdges = relations.Where(r => r.Kind == RelationKind.Isomorphism).ToList();
        foreach (var edge in isoEdges)
        {
            edge.FromLcm.Should().BeLessThan(edge.ToLcm);
            // No reverse edge should exist for the same pair.
            isoEdges.Should().NotContain(e => e.FromLcm == edge.ToLcm && e.ToLcm == edge.FromLcm);
        }
    }

    [Fact]
    public void Renormalized_subset_base_is_never_unity()
    {
        // Base 1/1 is just the literal subset case; it must be reported as LiteralSubset, not as a ren-subset edge.
        var families = DefaultFamilies();

        var relations = FamilyRelations.Compute(families);

        foreach (var r in relations.Where(r => r.Kind == RelationKind.RenormalizedSubset))
        {
            r.Base.Should().NotBe(new Fraction(1, 1));
        }
    }

    [Fact]
    public void Every_renormalized_subset_edge_actually_holds()
    {
        // Independent oracle: re-derive the renormalized subset relation for each emitted edge.
        var families = DefaultFamilies();
        var byLcm = families.ToDictionary(f => f.Lcm);

        var relations = FamilyRelations.Compute(families);

        foreach (var r in relations.Where(r => r.Kind == RelationKind.RenormalizedSubset))
        {
            var fa = byLcm[r.FromLcm].Fractions;
            var fb = byLcm[r.ToLcm].Fractions;
            var ren = Renormalization.Renormalize(fa, r.Base!.Value);
            var fbSet = new HashSet<Fraction>(fb);
            ren.All(fbSet.Contains).Should().BeTrue();
            ren.Count.Should().BeLessThanOrEqualTo(fb.Count);
        }
    }

    [Fact]
    public void Every_isomorphism_edge_actually_holds()
    {
        var families = DefaultFamilies();
        var byLcm = families.ToDictionary(f => f.Lcm);

        var relations = FamilyRelations.Compute(families);

        foreach (var r in relations.Where(r => r.Kind == RelationKind.Isomorphism))
        {
            var fa = byLcm[r.FromLcm].Fractions;
            var fb = byLcm[r.ToLcm].Fractions;
            var ren = Renormalization.Renormalize(fa, r.Base!.Value);
            ren.Count.Should().Be(fb.Count);
            var fbSet = new HashSet<Fraction>(fb);
            ren.All(fbSet.Contains).Should().BeTrue();
        }
    }

    [Fact]
    public void Lcm9_embeds_into_Lcm24_via_both_four_thirds_and_sixteen_ninths()
    {
        // LCM 9 sits inside LCM 24 at two exact renormalizations; every base gets its own edge.
        var families = DefaultFamilies();

        var relations = FamilyRelations.Compute(families);

        var bases = relations
            .Where(r => r.Kind == RelationKind.RenormalizedSubset && r.FromLcm == 9 && r.ToLcm == 24)
            .Select(r => r.Base)
            .ToList();
        bases.Should().BeEquivalentTo(
            new Fraction?[] { new Fraction(4, 3), new Fraction(16, 9) });
    }

    [Fact]
    public void Cluster_tolerance_zero_emits_no_approximate_edges()
    {
        var families = DefaultFamilies();
        var fractions = DefaultGoodFractions();

        var exactOnly = FamilyRelations.Compute(families, fractions, clusterTolerance: 0);

        exactOnly.Should().NotContain(r => r.Kind == RelationKind.ApproximateRenormalizedSubset);
        // Byte-for-byte identical to the no-arg overload.
        exactOnly.Should().BeEquivalentTo(FamilyRelations.Compute(families));
    }

    [Fact]
    public void Lcm18_approximately_embeds_into_Lcm24_via_sixteen_ninths_at_the_syntonic_comma()
    {
        // Renormalizing the whole lcm-18 family by 16/9 lands inside lcm-24 once the single inexact image
        // 27/16 is clustered onto 5/3 — a miss of exactly the syntonic comma, c = 1/161 in the bin metric.
        // (Base 9/5 does NOT work: 9/5 isn't in the lcm-18 family, and renormalizing by other members
        // throws an image outside every good-fraction bin.)
        var families = DefaultFamilies();
        var fractions = DefaultGoodFractions();
        var syntonicComma = 1.0 / 161;

        var relations = FamilyRelations.Compute(families, fractions, syntonicComma);

        var approx = relations
            .Where(r => r.Kind == RelationKind.ApproximateRenormalizedSubset && r.FromLcm == 18 && r.ToLcm == 24)
            .ToList();
        approx.Select(r => r.Base).Should().BeEquivalentTo(new Fraction?[] { new Fraction(16, 9) });
        approx.Single().MaxBinError!.Value.Should().BeApproximately(syntonicComma, 1e-9);
    }

    [Fact]
    public void Approximate_edges_switch_on_at_the_syntonic_comma_threshold()
    {
        var families = DefaultFamilies();
        var fractions = DefaultGoodFractions();

        bool HasLcm18To24(double tolerance) => FamilyRelations
            .Compute(families, fractions, tolerance)
            .Any(r => r.Kind == RelationKind.ApproximateRenormalizedSubset && r.FromLcm == 18 && r.ToLcm == 24);

        HasLcm18To24(0.006).Should().BeFalse();  // just below 1/161
        HasLcm18To24(0.007).Should().BeTrue();   // just above 1/161
    }

    private static IReadOnlyList<LcmFamily> DefaultFamilies()
    {
        var fractions = DefaultGoodFractions();
        return LcmFamilies.Compute(fractions, maxLcm: 24);
    }

    private static IReadOnlyList<Fraction> DefaultGoodFractions()
        => GoodFractions.Enumerate(maxSize: 24, maxPrime: 5);
}

