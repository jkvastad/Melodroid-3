namespace Melodroid_3.Music;

// A single sounding note, flattened from MIDI by MidiFileReader. Times are in ticks; NoteNumber is a
// raw MIDI pitch (folded mod 12 during chunking). Pure — no DryWetMidi types leak in here.
public readonly record struct NoteEvent(long StartTicks, long LengthTicks, int NoteNumber);

// One discrete time slot (or a merged run of identical adjacent slots) and the pitch-class key-set
// sounding across it. Beats are quarter-note beats (ticks / ticksPerQuarter); the span is [StartBeat,
// EndBeat). Keys are distinct pitch classes in [0, ktet), sorted ascending.
public readonly record struct SongChunk(int Index, double StartBeat, double EndBeat, IReadOnlyList<int> Keys);

// A chunk together with every LCM-family placement and named scale that contains its key-set.
public readonly record struct ChunkAnalysis(
    SongChunk Chunk,
    IReadOnlyList<KeySupersetRow> Placements,
    IReadOnlyList<ScaleRow> Scales);

// Pure MIDI-song analysis: discretize note events onto an onset-quantized grid, then map each chunk's
// key-set onto the derived LCM-family placements and named scales. No I/O — MidiFileReader supplies
// the NoteEvent list; this stays trivially testable with synthetic events.
public static class SongAnalysis
{
    // Slice note events onto a grid of `subdivisionsPerQuarter` slots per quarter note. A note sounds
    // in a slot iff its half-open tick span covers the slot's start. Empty (silent) slots are dropped.
    // With `mergeRepeats`, maximal runs of time-adjacent slots sharing an identical key-set collapse
    // into one chunk — light "where the harmony changes" preprocessing. The reference frequency is
    // left implicit: FindSupersets sweeps all anchors, so no absolute root is needed.
    public static IReadOnlyList<SongChunk> Chunk(
        IReadOnlyList<NoteEvent> events,
        int ticksPerQuarter,
        int subdivisionsPerQuarter,
        bool mergeRepeats)
    {
        if (events.Count == 0) return Array.Empty<SongChunk>();

        var stepTicks = ticksPerQuarter / (double)subdivisionsPerQuarter;
        var endTicks = events.Max(e => e.StartTicks + e.LengthTicks);
        var slotCount = (int)Math.Ceiling(endTicks / stepTicks);

        // Per non-empty slot: (slot index, sounding key-set).
        var slots = new List<(int Slot, IReadOnlyList<int> Keys)>();
        for (var i = 0; i < slotCount; i++)
        {
            var slotStart = i * stepTicks;
            var keys = events
                .Where(e => e.StartTicks <= slotStart && slotStart < e.StartTicks + e.LengthTicks)
                .Select(e => ((e.NoteNumber % 12) + 12) % 12)
                .Distinct()
                .OrderBy(k => k)
                .ToList();
            if (keys.Count > 0) slots.Add((i, keys));
        }

        var chunks = new List<SongChunk>();
        var index = 0;
        for (var s = 0; s < slots.Count; s++)
        {
            var startSlot = slots[s].Slot;
            var endSlot = startSlot;
            var keys = slots[s].Keys;

            if (mergeRepeats)
            {
                while (s + 1 < slots.Count
                    && slots[s + 1].Slot == endSlot + 1
                    && slots[s + 1].Keys.SequenceEqual(keys))
                {
                    endSlot = slots[s + 1].Slot;
                    s++;
                }
            }

            chunks.Add(new SongChunk(
                index++,
                startSlot / (double)subdivisionsPerQuarter,
                (endSlot + 1) / (double)subdivisionsPerQuarter,
                keys));
        }

        return chunks;
    }

    // For each chunk, the LCM-family placements (FindSupersets, exhaustive and sorted by extra→lcm→at)
    // and named-scale anchors (ContainingChord) that contain its key-set.
    public static IReadOnlyList<ChunkAnalysis> Analyze(
        IReadOnlyList<SongChunk> chunks,
        IReadOnlyList<LcmFamily> families,
        int ktet)
    {
        var result = new List<ChunkAnalysis>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var placements = Placements.FindSupersets(chunk.Keys, families, ktet);
            var scales = NamedScales.ContainingChord(chunk.Keys, ktet);
            result.Add(new ChunkAnalysis(chunk, placements, scales));
        }
        return result;
    }
}
