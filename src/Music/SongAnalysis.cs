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

// The progression bridge between two consecutive chunks: the ChordProgressions category for the
// ordered key-set pair plus the superset ("{Lcm}@{At}") and subset ("P_A·S·P_B") bridge labels.
// This is the cross-chunk structure a per-chunk containment list cannot show — e.g. a C→F→G→C loop
// reads as superset bridges 8@5, 24@0, 8@0 down the successive pairs.
public readonly record struct ChunkBridge(
    int FromIndex,
    int ToIndex,
    IReadOnlyList<int> FromKeys,
    IReadOnlyList<int> ToKeys,
    ProgressionCategory Category,
    IReadOnlyList<string> SupersetBridges,
    IReadOnlyList<string> SubsetBridges);

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

    // For each consecutive chunk pair, the progression bridge between their key-sets. Reuses
    // ChordProgressions.ComputePair (which works on arbitrary key-sets, not just triads) to classify
    // the pair and collect its triplets, then ChordProgressions.BridgeLabels to format them — so the
    // labels match the `progression` command exactly. collapseIsomorphic / poolMaximal declutter the
    // labels without changing the category (same semantics as `progression`).
    //
    // nameJunctions reframes the subset bridges as scales: each subset triplet's (Origin, Target)
    // pair is a junction scale (the stable union P_A ∪ P_B), which often lands on a named scale. In
    // that mode the Subset labels become those named junctions ("{Name}@{Anchor}", or the raw "{keys}"
    // when the union matches no catalog scale), deduped — a far smaller list than the raw triplets,
    // since the union ignores which S was chosen. Superset bridges are unaffected either way.
    public static IReadOnlyList<ChunkBridge> Bridges(
        IReadOnlyList<SongChunk> chunks,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        bool nameJunctions,
        int ktet)
    {
        var result = new List<ChunkBridge>(Math.Max(0, chunks.Count - 1));
        for (var i = 0; i + 1 < chunks.Count; i++)
        {
            var from = chunks[i];
            var to = chunks[i + 1];
            var (category, triplets) = ChordProgressions.ComputePair(
                from.Keys, to.Keys, families, minSubsetNotes, collapseIsomorphic, poolMaximal,
                CollapseFilter.None, ktet);
            var (superset, subset) = ChordProgressions.BridgeLabels(triplets);
            if (nameJunctions) subset = JunctionLabels(triplets, ktet);
            result.Add(new ChunkBridge(
                from.Index, to.Index, from.Keys, to.Keys, category, superset, subset));
        }
        return result;
    }

    // The named junction scales of a pair's subset triplets: one label per distinct (Origin, Target)
    // pair (the union ignores S, so the many triplets sharing a pair collapse to one), each the stable
    // union P_A ∪ P_B named via NamedScales.Match, or its raw "{keys}" when unnamed. Sorted for stable
    // display: named scales first (catalog order, then anchor), then unnamed key sets.
    private static IReadOnlyList<string> JunctionLabels(
        IReadOnlyList<ProgressionTriplet> triplets, int ktet)
    {
        var catalogOrder = NamedScales.Catalog
            .Select((s, i) => (s.Name, i))
            .ToDictionary(x => x.Name, x => x.i);

        var labels = new Dictionary<string, (int Rank, int Anchor, string Label)>();
        foreach (var t in triplets.Where(t => t.Principle == ProgressionPrinciple.Subset))
        {
            var key = $"{t.Origin.Lcm}@{t.Origin.At}|{t.Target.Lcm}@{t.Target.At}";
            if (labels.ContainsKey(key)) continue;

            var union = JunctionScales.UnionKeys(t.Origin, t.Target, stable: true, ktet);
            if (NamedScales.Match(union, ktet) is (string name, int anchor))
                labels[key] = (catalogOrder[name], anchor, $"{name}@{anchor}");
            else
                labels[key] = (catalogOrder.Count, 0, "{" + string.Join(" ", union) + "}");
        }

        return labels.Values
            .Distinct()
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Anchor)
            .ThenBy(x => x.Label, StringComparer.Ordinal)
            .Select(x => x.Label)
            .ToList();
    }
}
