namespace Melodroid_3.Music;

// A single sounding note, flattened from MIDI by MidiFileReader. Times are in ticks; NoteNumber is a
// raw MIDI pitch (folded mod 12 during chunking). Pure — no DryWetMidi types leak in here.
public readonly record struct NoteEvent(long StartTicks, long LengthTicks, int NoteNumber);

// One discrete time slot (or a merged run of identical adjacent slots) and the pitch-class key-set
// sounding across it. Beats are quarter-note beats (ticks / ticksPerQuarter); the span is [StartBeat,
// EndBeat). Keys are distinct pitch classes in [0, ktet), sorted ascending.
public readonly record struct SongChunk(int Index, double StartBeat, double EndBeat, IReadOnlyList<int> Keys);

// A chunk together with every LCM-family placement and composite scale that contains its key-set. The
// Scales are derived from first principles (CompositeScales — the subset principle), not a curated
// catalog; traditional names are an optional cosmetic relabel at render time (NamedScales.Match).
public readonly record struct ChunkAnalysis(
    SongChunk Chunk,
    IReadOnlyList<KeySupersetRow> Placements,
    IReadOnlyList<CompositeScale> Scales);

// A maximal run of consecutive chunks sharing ≥1 common label (scale or placement). The common
// labels are the Witnesses; Length is the chain length in chunks. Empty Witnesses = a chunk no
// label contains (singleton, unexplained). The span is [StartBeat, EndBeat) in quarter-note beats.
public readonly record struct LabelSegment(
    int FromIndex,
    int ToIndex,
    double StartBeat,
    double EndBeat,
    int Length,
    IReadOnlyList<string> Witnesses);

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
    // and the composite scales (CompositeScales — the subset principle, |S| ≥ minSubsetNotes) that
    // contain its key-set. Single placements already live in the Placements column, so Scales lists
    // composites only. The composite catalog is deduped to one representative per key set first, so the
    // same scale carries the same label in every chunk (segments compare labels across chunks).
    public static IReadOnlyList<ChunkAnalysis> Analyze(
        IReadOnlyList<SongChunk> chunks,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        int ktet)
    {
        var composites = DedupeByKeySet(CompositeScales.Enumerate(
            families, minSubsetNotes, stable: true, collapseIsomorphic, poolMaximal,
            dedupeTransposition: false, ktet));

        var result = new List<ChunkAnalysis>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var placements = Placements.FindSupersets(chunk.Keys, families, ktet);
            var chord = new HashSet<int>(chunk.Keys);
            var scales = composites.Where(c => chord.IsSubsetOf(c.Keys)).ToList();
            result.Add(new ChunkAnalysis(chunk, placements, scales));
        }
        return result;
    }

    // One composite scale per distinct key set (CompositeScales.Enumerate can emit several decompositions
    // of the same key set across anchors), chosen by lowest constituent LCMs/anchors, then largest
    // bridge — so a given scale always labels identically. Mirrors CompositeScales' RepRank ordering.
    private static IReadOnlyList<CompositeScale> DedupeByKeySet(IReadOnlyList<CompositeScale> scales)
    {
        (int, int, int, int, int) Rank(CompositeScale s) =>
            (s.PA.Lcm, s.PA.At, s.PB.Lcm, s.PB.At, -s.S.Keys.Count);
        var best = new Dictionary<string, CompositeScale>();
        foreach (var s in scales)
        {
            var sig = string.Join(",", s.Keys);
            if (!best.TryGetValue(sig, out var current) || Rank(s).CompareTo(Rank(current)) < 0)
                best[sig] = s;
        }
        return best.Values.OrderBy(s => s.Keys.Count).ThenBy(s => string.Join(",", s.Keys)).ToList();
    }

    // For each consecutive chunk pair, the progression bridge between their key-sets. Reuses
    // ChordProgressions.ComputePair (which works on arbitrary key-sets, not just triads) to classify
    // the pair and collect its triplets, then ChordProgressions.BridgeLabels to format them — so the
    // labels match the `progression` command exactly. collapseIsomorphic / poolMaximal declutter the
    // labels without changing the category (same semantics as `progression`).
    //
    // nameComposites reframes the subset bridges as scales: each subset triplet's (Origin, Target)
    // pair is a composite scale (the stable union P_A ∪ P_B), which often lands on a named scale. In
    // that mode the Subset labels become those named composites ("{Name}@{Anchor}", or the raw "{keys}"
    // when the union matches no catalog scale), deduped — a far smaller list than the raw triplets,
    // since the union ignores which S was chosen. Superset bridges are unaffected either way.
    public static IReadOnlyList<ChunkBridge> Bridges(
        IReadOnlyList<SongChunk> chunks,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        bool nameComposites,
        bool escalate,
        int ktet)
    {
        var catalog = CompositeCatalog(escalate, families, minSubsetNotes, collapseIsomorphic, poolMaximal, ktet);
        var result = new List<ChunkBridge>(Math.Max(0, chunks.Count - 1));
        for (var i = 0; i + 1 < chunks.Count; i++)
        {
            var from = chunks[i];
            var to = chunks[i + 1];
            result.Add(Pair(
                from.Index, to.Index, from.Keys, to.Keys,
                families, minSubsetNotes, collapseIsomorphic, poolMaximal, nameComposites, escalate, catalog, ktet));
        }
        return result;
    }

    // The same progression bridges as Bridges, but between consecutive *segments* rather than chunks.
    // Each segment's representative key-set is the union of its chunks' sounding keys (a 1-chunk
    // segment reduces to that chunk's keys, so this generalizes Bridges); from there the machinery is
    // identical (ChordProgressions.ComputePair → BridgeLabels, optional CompositeLabels). The emitted
    // ChunkBridge records key FromIndex/ToIndex on the segment ordinal (0,1,2,…) and carry the two
    // segment unions as From/ToKeys. Surfaces cross-segment loops a per-chunk bridges table hides —
    // e.g. an 8@4-segment ↔ 15@11-segment alternation reads down the Superset column.
    public static IReadOnlyList<ChunkBridge> SegmentBridges(
        IReadOnlyList<SongChunk> chunks,
        IReadOnlyList<LabelSegment> segments,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        bool nameComposites,
        bool escalate,
        int ktet)
    {
        var catalog = CompositeCatalog(escalate, families, minSubsetNotes, collapseIsomorphic, poolMaximal, ktet);
        var unions = segments.Select(s => SegmentUnion(chunks, s)).ToList();
        var result = new List<ChunkBridge>(Math.Max(0, segments.Count - 1));
        for (var i = 0; i + 1 < segments.Count; i++)
            result.Add(Pair(
                i, i + 1, unions[i], unions[i + 1],
                families, minSubsetNotes, collapseIsomorphic, poolMaximal, nameComposites, escalate, catalog, ktet));
        return result;
    }

    // Bridges between consecutive *scale* segments, read through constituents rather than a pooled
    // union. A scale segment is long, so unioning its chunks (SegmentBridges) pools a whole phrase of
    // melody into a chromatic key-set no placement contains, collapsing almost every pair to Neither.
    // Instead each segment's union is mapped to its constituent pool (CompositeProgressions.ConstituentPool
    // — the single placements and composite-scale constituents containing the union); the bridge is the
    // composite placement progression between the two pools (CompositeProgressions.LinkPools — the SAME
    // calculation `table progression`'s composite tier uses): a shared placement (pivot → superset)
    // and/or a subset-S join (→ P_A·S·P_B). A side with no constituents (empty-witness singleton) falls
    // back to the SegmentBridges union→ComputePair path so output is never incoherent.
    public static IReadOnlyList<ChunkBridge> ScaleSegmentBridges(
        IReadOnlyList<SongChunk> chunks,
        IReadOnlyList<LabelSegment> segments,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        bool nameComposites,
        bool escalate,
        int ktet)
    {
        // The shared composite catalog, used both to pool each segment's constituents and (when
        // escalate) for the union-path fallback below. Built once.
        var catalog = CompositeProgressions.Catalog(families, minSubsetNotes, collapseIsomorphic, poolMaximal, ktet);
        var unions = segments.Select(s => SegmentUnion(chunks, s)).ToList();
        var pools = unions
            .Select(u => CompositeProgressions.ConstituentPool(u, families, minSubsetNotes, ktet, catalog))
            .ToList();

        var result = new List<ChunkBridge>(Math.Max(0, segments.Count - 1));
        for (var i = 0; i + 1 < segments.Count; i++)
        {
            var lset = pools[i];
            var rset = pools[i + 1];
            if (lset.Count == 0 || rset.Count == 0)
            {
                // A side has no constituents (empty-witness singleton) — fall back to the plain
                // union→ComputePair bridge so output is never incoherent.
                result.Add(Pair(
                    i, i + 1, unions[i], unions[i + 1],
                    families, minSubsetNotes, collapseIsomorphic, poolMaximal, nameComposites, escalate, catalog, ktet));
                continue;
            }

            // The composite placement progression between the two segments' constituent placements —
            // the SAME CompositeProgressions.LinkPools `table progression` uses for its composite tier.
            var (category, triplets) = CompositeProgressions.LinkPools(
                lset, rset, families, minSubsetNotes, collapseIsomorphic, poolMaximal, ktet);
            var (superset, subset) = ChordProgressions.BridgeLabels(triplets);
            if (nameComposites) subset = CompositeProgressions.CompositeLabels(triplets, ktet);
            result.Add(new ChunkBridge(i, i + 1, unions[i], unions[i + 1], category, superset, subset));
        }
        return result;
    }

    // The union of sounding keys across a segment's chunks — distinct pitch classes, sorted ascending.
    // Chunk .Index is monotonic and preserved under --from/--to, so [FromIndex, ToIndex] selects the
    // segment's chunks.
    private static IReadOnlyList<int> SegmentUnion(IReadOnlyList<SongChunk> chunks, LabelSegment segment)
        => chunks
            .Where(c => c.Index >= segment.FromIndex && c.Index <= segment.ToIndex)
            .SelectMany(c => c.Keys)
            .Distinct()
            .OrderBy(k => k)
            .ToList();

    // Classify one ordered key-set pair into a progression bridge (shared by Bridges and
    // SegmentBridges so both label bridges identically). fromIdx/toIdx are the display indices
    // (chunk .Index for chunks, segment ordinal for segments).
    private static ChunkBridge Pair(
        int fromIdx,
        int toIdx,
        IReadOnlyList<int> fromKeys,
        IReadOnlyList<int> toKeys,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        bool nameComposites,
        bool escalate,
        IReadOnlyList<CompositeScale> compositeCatalog,
        int ktet)
    {
        var (category, triplets) = ChordProgressions.ComputePair(
            fromKeys, toKeys, families, minSubsetNotes, collapseIsomorphic, poolMaximal,
            CollapseFilter.None, ktet);
        var (superset, subset) = ChordProgressions.BridgeLabels(triplets);
        if (nameComposites) subset = CompositeProgressions.CompositeLabels(triplets, ktet);
        // Escalation (opt-in via escalate): when the placement graph has no edge (Neither) the move is
        // carried by a composite placement progression — origin and target each map to a composite
        // scale, linked by a placement progression between their constituent placements
        // (CompositeProgressions.ComputeComposite, the same calculation `table progression`'s composite
        // tier uses). The link's superset/subset labels are prefixed "↑" to mark the escalation beyond
        // the plain placement graph; the category stays Neither. nameComposites only chooses the subset
        // label form (shorthand scale name vs raw P_A·S·P_B).
        if (escalate && category == ProgressionCategory.Neither)
        {
            var (_, link) = CompositeProgressions.ComputeComposite(
                fromKeys, toKeys, families, minSubsetNotes, collapseIsomorphic, poolMaximal, ktet, compositeCatalog);
            var (linkSuperset, linkSubset) = ChordProgressions.BridgeLabels(link);
            if (nameComposites) linkSubset = CompositeProgressions.CompositeLabels(link, ktet);
            superset = linkSuperset.Select(s => $"↑{s}").ToList();
            subset = linkSubset.Select(s => $"↑{s}").ToList();
        }
        return new ChunkBridge(fromIdx, toIdx, fromKeys, toKeys, category, superset, subset);
    }

    // The anchored composite catalog used for escalation, computed once per bridges table. Only the
    // escalation path consumes it, so it is built only when escalate is on. Delegates to
    // CompositeProgressions.Catalog so the Enumerate parameters stay single-sourced with `table progression`.
    private static IReadOnlyList<CompositeScale> CompositeCatalog(
        bool escalate,
        IReadOnlyList<LcmFamily> families,
        int minSubsetNotes,
        bool collapseIsomorphic,
        bool poolMaximal,
        int ktet)
        => escalate
            ? CompositeProgressions.Catalog(families, minSubsetNotes, collapseIsomorphic, poolMaximal, ktet)
            : Array.Empty<CompositeScale>();

    // The composite-scale labels containing a chunk's key-set. Default form is the raw constituents
    // "P_A·S·P_B" (as `table composite-scales` / `chord-melody` print); with nameComposites a composite
    // whose key set lands on a catalog scale is relabelled "{Shorthand}@{Anchor}" (cosmetic only, via
    // NamedScales.Match). Shared with the per-chunk table so both views label scales identically.
    public static IReadOnlyList<string> ScaleLabels(ChunkAnalysis a, bool nameComposites, int ktet) => a.Scales
        .Select(s => CompositeLabel(s, nameComposites, ktet))
        .ToList();

    // One composite scale's label: its cosmetic catalog name when nameComposites and the key set matches
    // one, else the raw "P_A·S·P_B" constituents.
    private static string CompositeLabel(CompositeScale s, bool nameComposites, int ktet) =>
        nameComposites && NamedScales.Match(s.Keys, ktet) is { } m
            ? $"{NamedScales.Shorthand(m.Name)}@{m.Anchor}"
            : $"{s.PA.Lcm}@{s.PA.At}·{s.S.Lcm}@{s.S.At}·{s.PB.Lcm}@{s.PB.At}";

    // The LCM-family placement labels ("{Lcm}@{At}") containing a chunk's key-set, isomorphic-
    // collapsed to lowest-LCM representatives. Shared with the per-chunk table's Placements column.
    // With `stable`: (1) drop unstable placements (lcm-15 / lcm-20) whose collapsing key is sounding
    // in the chunk — they have collapsed onto a simpler lcm-24 pattern (the DropCollapsed rule used by
    // `progression`); and (2) relabel the surviving collapsing-key placements into
    // stable form "{Lcm}s@{At}" (15s@ / 20s@, the stable-form marker) — step 1 has
    // already removed those whose collapsing key sounds, so every survivor with a collapsing key is
    // legitimately the stable sub-pattern. Reuses Placements.CollapsingKey as the single source.
    public static IReadOnlyList<string> PlacementLabels(ChunkAnalysis a, bool stable, int ktet)
    {
        var rows = a.Placements;
        if (stable)
        {
            var chord = new HashSet<int>(a.Chunk.Keys);
            rows = rows
                .Where(r => Placements.CollapsingKey(r.Placement, ktet) is not int ck || !chord.Contains(ck))
                .ToList();
        }
        return Placements.CollapseIsomorphic(rows)
            .Select(r => stable && Placements.CollapsingKey(r.Placement, ktet) is not null
                ? $"{r.Placement.Lcm}s@{r.Placement.At}"
                : $"{r.Placement.Lcm}@{r.Placement.At}")
            .ToList();
    }

    // Greedy minimum-count partition of the chunk timeline into contiguous segments, each sharing
    // ≥1 common label. Starting a segment at chunk i, extend while the running intersection of the
    // per-chunk label sets stays non-empty, then cut — this minimizes the number of segments and so
    // the longest segment is the longest unbroken chain of a single label. A chunk whose label set
    // is empty is its own singleton segment with no witness. Witnesses are ordered by first
    // appearance (the starting chunk's order) for a deterministic, stable rendering.
    public static IReadOnlyList<LabelSegment> GreedySegments(
        IReadOnlyList<SongChunk> chunks,
        IReadOnlyList<IReadOnlyList<string>> labelsPerChunk)
    {
        if (chunks.Count != labelsPerChunk.Count)
            throw new ArgumentException("chunks and labelsPerChunk must align 1:1.");

        var segments = new List<LabelSegment>();
        var i = 0;
        while (i < chunks.Count)
        {
            var common = new HashSet<string>(labelsPerChunk[i]);
            var j = i;
            while (common.Count > 0 && j + 1 < chunks.Count)
            {
                var next = new HashSet<string>(labelsPerChunk[j + 1]);
                next.IntersectWith(common);
                if (next.Count == 0) break;
                common = next;
                j++;
            }

            // Order surviving witnesses by their appearance in the starting chunk's label list.
            var witnesses = labelsPerChunk[i].Where(common.Contains).ToList();
            segments.Add(new LabelSegment(
                chunks[i].Index,
                chunks[j].Index,
                chunks[i].StartBeat,
                chunks[j].EndBeat,
                j - i + 1,
                witnesses));
            i = j + 1;
        }

        return segments;
    }
}
