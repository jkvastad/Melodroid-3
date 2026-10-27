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

        // Naming collapses the many triplets to a smaller set (the union ignores which S was chosen).
        named.SubsetBridges.Count.Should().BeLessThanOrEqualTo(raw.SubsetBridges.Count);
    }
}
