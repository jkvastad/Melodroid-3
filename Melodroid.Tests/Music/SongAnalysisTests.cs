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
}
