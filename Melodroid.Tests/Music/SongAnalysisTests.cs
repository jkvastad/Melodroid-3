using AwesomeAssertions;
using Melodroid_3.Music;

namespace Melodroid_3.Tests.Music;

public class SongAnalysisTests
{
    private const int Ktet = 12;
    private const int Tpq = 480;         // ticks per quarter note
    private const int Subdivision = 4;   // sixteenth-note grid → 120-tick slots

    // Family data under the standard good-fraction defaults, as production builds it.
    private static IReadOnlyList<LcmFamily> Families() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);

    // A note starting on slot `slot` (120-tick grid) lasting `slots` grid slots, at MIDI pitch `note`.
    private static NoteEvent Note(int slot, int slots, int note) =>
        new(slot * (Tpq / Subdivision), slots * (Tpq / Subdivision), note);

    [Fact]
    public void Chunk_EmptyEvents_ReturnsEmpty()
    {
        SongAnalysis.Chunk(Array.Empty<NoteEvent>(), Tpq, Subdivision, mergeRepeats: true)
            .Should().BeEmpty();
    }

    [Fact]
    public void Chunk_SustainedTriad_MergesToOneChunkSpanningTheWholeQuarter()
    {
        // C major triad (60/64/67) held across the first quarter note (4 sixteenth slots).
        var events = new[] { Note(0, 4, 60), Note(0, 4, 64), Note(0, 4, 67) };

        var chunks = SongAnalysis.Chunk(events, Tpq, Subdivision, mergeRepeats: true);

        chunks.Should().ContainSingle();
        chunks[0].Keys.Should().Equal(0, 4, 7);
        chunks[0].StartBeat.Should().Be(0);
        chunks[0].EndBeat.Should().Be(1);   // slots 0..3 → (3+1)/4 quarters
    }

    [Fact]
    public void Chunk_NoteChange_ProducesTwoChunks()
    {
        // {0,4,7} for the first quarter, then {2,5,9} for the second.
        var events = new[]
        {
            Note(0, 4, 60), Note(0, 4, 64), Note(0, 4, 67),
            Note(4, 4, 62), Note(4, 4, 65), Note(4, 4, 69),
        };

        var chunks = SongAnalysis.Chunk(events, Tpq, Subdivision, mergeRepeats: true);

        chunks.Should().HaveCount(2);
        chunks[0].Keys.Should().Equal(0, 4, 7);
        chunks[1].Keys.Should().Equal(2, 5, 9);
        chunks[1].StartBeat.Should().Be(1);
        chunks[1].EndBeat.Should().Be(2);
    }

    [Fact]
    public void Chunk_MergeRepeatsOff_KeepsEverySlot()
    {
        var events = new[] { Note(0, 4, 60), Note(0, 4, 64), Note(0, 4, 67) };

        var chunks = SongAnalysis.Chunk(events, Tpq, Subdivision, mergeRepeats: false);

        chunks.Should().HaveCount(4);
        chunks.Should().OnlyContain(c => c.Keys.SequenceEqual(new[] { 0, 4, 7 }));
        chunks.Select(c => c.Index).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void Chunk_SilentSlotBreaksAMergeRun()
    {
        // Identical key-set on slot 0 and slot 2 with slot 1 silent: the gap prevents merging even
        // though the keys match, so the two sounding regions stay distinct chunks.
        var events = new[] { Note(0, 1, 60), Note(2, 1, 60) };

        var chunks = SongAnalysis.Chunk(events, Tpq, Subdivision, mergeRepeats: true);

        chunks.Should().HaveCount(2);
        chunks.Should().OnlyContain(c => c.Keys.SequenceEqual(new[] { 0 }));
        chunks[0].StartBeat.Should().Be(0);
        chunks[1].StartBeat.Should().Be(0.5);   // slot 2 on a sixteenth grid = half a quarter
    }

    [Fact]
    public void Chunk_NoteOnsetLandsInItsOwnSlotOnly()
    {
        // A note confined to slot 1 (ticks [120, 240)) must appear only in that slot, not slot 0 or 2.
        var events = new[] { Note(1, 1, 60) };

        var chunks = SongAnalysis.Chunk(events, Tpq, Subdivision, mergeRepeats: false);

        chunks.Should().ContainSingle();
        chunks[0].StartBeat.Should().Be(0.25);
        chunks[0].Keys.Should().Equal(0);
    }

    [Fact]
    public void Analyze_MajorTriadChunk_FindsLcm24At0AndMajorScale()
    {
        var chunk = new SongChunk(0, 0, 1, new[] { 0, 4, 7 });

        var analysis = SongAnalysis.Analyze(new[] { chunk }, Families(), Ktet).Single();

        analysis.Placements.Should().Contain(r => r.Placement.Lcm == 24 && r.Placement.At == 0);
        analysis.Placements.Should().OnlyContain(r => new HashSet<int> { 0, 4, 7 }.IsSubsetOf(r.Placement.Keys));
        analysis.Scales.Should().Contain(s => s.Name == "major" && s.Anchor == 0);
    }

    // The C F G loop (C → F → G → C) is the motivating example: its superset bridges are 8@5, 24@0,
    // 8@0 down the successive pairs — the loop a per-chunk containment list cannot show. See
    // website/docs/music/progression-principles.mdx "C F G".
    [Fact]
    public void Bridges_CFG_loop_surfaces_the_superset_chain()
    {
        var chunks = new[]
        {
            new SongChunk(0, 0, 1, new[] { 0, 4, 7 }),   // C
            new SongChunk(1, 1, 2, new[] { 0, 5, 9 }),   // F
            new SongChunk(2, 2, 3, new[] { 2, 7, 11 }),  // G
            new SongChunk(3, 3, 4, new[] { 0, 4, 7 }),   // C
        };

        var bridges = SongAnalysis.Bridges(
            chunks, Families(), minSubsetNotes: 2, collapseIsomorphic: false, poolMaximal: false,
            nameJunctions: false, Ktet);

        bridges.Should().HaveCount(3);
        bridges[0].SupersetBridges.Should().Contain("8@5");   // C → F
        bridges[1].SupersetBridges.Should().Contain("24@0");  // F → G
        bridges[2].SupersetBridges.Should().Contain("8@0");   // G → C
        bridges.Should().OnlyContain(b =>
            b.Category == ProgressionCategory.Superset || b.Category == ProgressionCategory.Both);
    }

    [Fact]
    public void Bridges_FewerThanTwoChunks_ReturnsEmpty()
    {
        var chunks = new[] { new SongChunk(0, 0, 1, new[] { 0, 4, 7 }) };

        SongAnalysis.Bridges(
            chunks, Families(), minSubsetNotes: 2, collapseIsomorphic: false, poolMaximal: false,
            nameJunctions: false, Ktet)
            .Should().BeEmpty();
    }

    // --name-junctions reframes the subset bridges as scales: each raw P_A·S·P_B triplet becomes the
    // named junction scale of its (P_A, P_B) stable union. Driving the same chunk pair both ways, the
    // naming must never change the category and must emit only "{name}@{anchor}" scale names or bare
    // "{keys}" sets — never the "·"-joined triplets the default produces. (The deterministic
    // "union is harmonic minor" path is covered in JunctionScalesTests + NamedScalesTests.)
    [Fact]
    public void Bridges_NameJunctions_ReformatsSubsetBridgesWithoutChangingCategory()
    {
        // Two major-scale-adjacent triads that share ≥4-key placement intersections, so the subset
        // principle fires and there are bridges to reformat.
        var chunks = new[]
        {
            new SongChunk(0, 0, 1, new[] { 0, 4, 7 }),   // C major
            new SongChunk(1, 1, 2, new[] { 2, 7, 11 }),  // G major
        };

        var raw = SongAnalysis.Bridges(
            chunks, Families(), minSubsetNotes: 4, collapseIsomorphic: false, poolMaximal: false,
            nameJunctions: false, Ktet)[0];
        var named = SongAnalysis.Bridges(
            chunks, Families(), minSubsetNotes: 4, collapseIsomorphic: false, poolMaximal: false,
            nameJunctions: true, Ktet)[0];

        named.Category.Should().Be(raw.Category);                 // naming never reclassifies
        named.SupersetBridges.Should().Equal(raw.SupersetBridges); // superset column untouched

        // Raw subset labels are P_A·S·P_B triplets; named ones are scale names or bare key sets.
        raw.SubsetBridges.Should().OnlyContain(s => s.Contains('·'));
        named.SubsetBridges.Should().OnlyContain(s => !s.Contains('·'));
        named.SubsetBridges.Should().OnlyContain(s => s.Contains('@') || s.StartsWith("{"));

        // Named scales use the compact shorthands, never the full catalog words.
        named.SubsetBridges.Should().OnlyContain(s =>
            !s.Contains("minor") && !s.Contains("major") && !s.Contains("bebop") && !s.Contains("blues"));

        // Naming collapses the many triplets to a smaller set (the union ignores which S was chosen).
        named.SubsetBridges.Count.Should().BeLessThanOrEqualTo(raw.SubsetBridges.Count);
    }

    private static KeySupersetRow Row(int lcm, int at, params int[] keys) =>
        new(new Placement(lcm, at, keys), Array.Empty<Fraction>(), 0);

    // --stable-placements drops an unstable lcm-15 / lcm-20 placement whose collapsing key is sounding
    // in the chunk (the DropCollapsed rule) and relabels the survivors into stable form (15s@ / 20s@).
    // For a {0,4,7} chunk, 15@4 collapses on key 0 (in the chunk) and disappears under stable, while
    // 15@7 collapses on key 3 (not sounding) and survives — shown as 15s@7. A non-collapsing lcm-24
    // placement keeps its plain label in both modes. Default (stable:false) lists everything plainly.
    [Fact]
    public void PlacementLabels_StableDropsCollapsedAndRelabelsSurvivors()
    {
        var chunk = new SongChunk(0, 0, 1, new[] { 0, 4, 7 });
        var analysis = new ChunkAnalysis(chunk, new[]
        {
            Row(15, 4, 0, 2, 4, 7, 9),      // lcm-15, collapsing key 0 ∈ chunk → unstable
            Row(15, 7, 0, 4, 6, 7, 9),      // lcm-15, collapsing key 3 ∉ chunk → stable
            Row(20, 1, 0, 4, 7, 10),        // lcm-20, collapsing key 4 ∈ chunk → unstable
            Row(20, 2, 0, 4, 7, 11),        // lcm-20, collapsing key 5 ∉ chunk → stable
            Row(24, 0, 0, 2, 4, 5, 7, 9),   // no collapsing key → always plain
        }, Array.Empty<ScaleRow>());

        SongAnalysis.PlacementLabels(analysis, stable: false, Ktet)
            .Should().BeEquivalentTo("15@4", "15@7", "20@1", "20@2", "24@0");
        SongAnalysis.PlacementLabels(analysis, stable: true, Ktet)
            .Should().BeEquivalentTo("15s@7", "20s@2", "24@0");
    }

    // A placeholder chunk at timeline position i: Index/StartBeat/EndBeat come from the position,
    // Keys are irrelevant to GreedySegments (it works off the supplied label lists).
    private static SongChunk Slot(int i) => new(i, i, i + 1, Array.Empty<int>());

    [Fact]
    public void GreedySegments_AllChunksShareOneLabel_SingleSegment()
    {
        var chunks = new[] { Slot(0), Slot(1), Slot(2) };
        var labels = new IReadOnlyList<string>[]
        {
            new[] { "M@0", "M@5" },
            new[] { "M@0", "hm@2" },
            new[] { "M@0" },
        };

        var segments = SongAnalysis.GreedySegments(chunks, labels);

        segments.Should().ContainSingle();
        segments[0].FromIndex.Should().Be(0);
        segments[0].ToIndex.Should().Be(2);
        segments[0].Length.Should().Be(3);
        segments[0].StartBeat.Should().Be(0);
        segments[0].EndBeat.Should().Be(3);
        // Only the label common to every chunk survives as a witness.
        segments[0].Witnesses.Should().Equal("M@0");
    }

    [Fact]
    public void GreedySegments_CutsWhereNoCommonLabelRemains_MinimumCount()
    {
        // chunk0 needs A, chunk2 needs B, chunk1 shares both: no single label spans all three, so the
        // minimum is two segments. Greedy extends the first run through the ambiguous middle chunk.
        var chunks = new[] { Slot(0), Slot(1), Slot(2) };
        var labels = new IReadOnlyList<string>[]
        {
            new[] { "A" },
            new[] { "A", "B" },
            new[] { "B" },
        };

        var segments = SongAnalysis.GreedySegments(chunks, labels);

        segments.Should().HaveCount(2);
        segments[0].FromIndex.Should().Be(0);
        segments[0].ToIndex.Should().Be(1);
        segments[0].Witnesses.Should().Equal("A");
        segments[1].FromIndex.Should().Be(2);
        segments[1].ToIndex.Should().Be(2);
        segments[1].Length.Should().Be(1);
        segments[1].Witnesses.Should().Equal("B");
    }

    [Fact]
    public void GreedySegments_EmptyLabelSet_IsAnUnexplainedSingleton()
    {
        var chunks = new[] { Slot(0), Slot(1), Slot(2) };
        var labels = new IReadOnlyList<string>[]
        {
            new[] { "M@0" },
            Array.Empty<string>(),   // no scale/placement contains this chunk
            new[] { "M@0" },
        };

        var segments = SongAnalysis.GreedySegments(chunks, labels);

        segments.Should().HaveCount(3);
        segments[1].FromIndex.Should().Be(1);
        segments[1].ToIndex.Should().Be(1);
        segments[1].Witnesses.Should().BeEmpty();
    }

    [Fact]
    public void GreedySegments_WitnessesAreTheIntersectionAcrossTheWholeSegment()
    {
        // "hm@2" appears in chunks 0 and 1 but not 2; it must not survive as a witness of the segment.
        var chunks = new[] { Slot(0), Slot(1), Slot(2) };
        var labels = new IReadOnlyList<string>[]
        {
            new[] { "M@0", "hm@2" },
            new[] { "M@0", "hm@2" },
            new[] { "M@0" },
        };

        var segments = SongAnalysis.GreedySegments(chunks, labels);

        segments.Should().ContainSingle();
        segments[0].Witnesses.Should().Equal("M@0");
        segments[0].Witnesses.Should().NotContain("hm@2");
    }
}
