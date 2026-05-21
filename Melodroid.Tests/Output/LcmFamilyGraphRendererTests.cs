using FluentAssertions;
using Melodroid_3.Output;

namespace Melodroid_3.Tests.Output;

public class LcmFamilyGraphRendererTests
{
    [Fact]
    public void Singleton_lcm_to_singleton_lcm_yields_one_class_edge()
    {
        // L=1 → class 1, L=2 → class 2. Single family edge 1→2 collapses to one class edge 1→2.
        var lcmToClass = new Dictionary<int, int> { [1] = 1, [2] = 2 };
        var familyEdges = new[] { (FromLcm: 1, ToLcm: 2) };

        var result = LcmFamilyGraphRenderer.CollapseAndReduce(familyEdges, lcmToClass);

        result.Should().BeEquivalentTo(new[] { (From: 1, To: 2) });
    }

    [Fact]
    public void Parallel_family_edges_between_same_class_pair_collapse_to_one()
    {
        // L=3 and L=4 share class 1; L=5 and L=6 share class 2. Two family edges (3→5, 4→6) → one class edge 1→2.
        var lcmToClass = new Dictionary<int, int> { [3] = 1, [4] = 1, [5] = 2, [6] = 2 };
        var familyEdges = new[] { (FromLcm: 3, ToLcm: 5), (FromLcm: 4, ToLcm: 6) };

        var result = LcmFamilyGraphRenderer.CollapseAndReduce(familyEdges, lcmToClass);

        result.Should().BeEquivalentTo(new[] { (From: 1, To: 2) });
    }

    [Fact]
    public void Intra_class_family_edge_is_dropped()
    {
        // L=3 and L=4 are in the same class 1. A family edge 3→4 collapses to a self-loop and should disappear.
        var lcmToClass = new Dictionary<int, int> { [3] = 1, [4] = 1 };
        var familyEdges = new[] { (FromLcm: 3, ToLcm: 4) };

        var result = LcmFamilyGraphRenderer.CollapseAndReduce(familyEdges, lcmToClass);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Transitive_class_edge_is_hasse_reduced()
    {
        // Class edges A→B, B→C, A→C of the same kind. The direct A→C should be dropped.
        var lcmToClass = new Dictionary<int, int> { [1] = 1, [2] = 2, [3] = 3 };
        var familyEdges = new[]
        {
            (FromLcm: 1, ToLcm: 2),
            (FromLcm: 2, ToLcm: 3),
            (FromLcm: 1, ToLcm: 3),
        };

        var result = LcmFamilyGraphRenderer.CollapseAndReduce(familyEdges, lcmToClass);

        result.Should().BeEquivalentTo(new[]
        {
            (From: 1, To: 2),
            (From: 2, To: 3),
        });
    }

    [Fact]
    public void Different_edge_kinds_are_reduced_independently()
    {
        // Even when a kind-B chain A→B→C exists, a kind-A direct edge A→C must survive
        // because CollapseAndReduce is called separately per kind.
        var lcmToClass = new Dictionary<int, int> { [1] = 1, [2] = 2, [3] = 3 };

        var literalEdges = new[] { (FromLcm: 1, ToLcm: 3) };
        var renSubsetEdges = new[]
        {
            (FromLcm: 1, ToLcm: 2),
            (FromLcm: 2, ToLcm: 3),
        };

        var literalResult = LcmFamilyGraphRenderer.CollapseAndReduce(literalEdges, lcmToClass);
        var renSubsetResult = LcmFamilyGraphRenderer.CollapseAndReduce(renSubsetEdges, lcmToClass);

        literalResult.Should().BeEquivalentTo(new[] { (From: 1, To: 3) });
        renSubsetResult.Should().BeEquivalentTo(new[]
        {
            (From: 1, To: 2),
            (From: 2, To: 3),
        });
    }
}
