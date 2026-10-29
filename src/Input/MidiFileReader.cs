using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using NoteEvent = Melodroid_3.Music.NoteEvent;

namespace Melodroid_3.Input;

// The single place DryWetMidi is referenced. Reads a MIDI file into the pure NoteEvent list that
// SongAnalysis consumes, the file's ticks-per-quarter-note (the beat unit), and the tempo map as a
// plain (tick, µs-per-quarter) list so no DryWetMidi type leaks out. Note on/off pairing and
// channel/track structure are still flattened away — SongAnalysis works only on
// (startTicks, lengthTicks, noteNumber) triples folded mod 12 — but tempo is retained so exporters
// (e.g. the Reaper CSV) can convert ticks to wall-clock seconds.
public static class MidiFileReader
{
    // Standard MIDI default when no SetTempo event precedes a region: 120 BPM.
    private const long DefaultMicrosecondsPerQuarter = 500_000;

    public static (IReadOnlyList<NoteEvent> Events, int TicksPerQuarter,
                   IReadOnlyList<(long Tick, long MicrosecondsPerQuarter)> Tempo) Read(string path)
    {
        var midiFile = MidiFile.Read(path);

        if (midiFile.TimeDivision is not TicksPerQuarterNoteTimeDivision tpqn)
            throw new NotSupportedException(
                "Only ticks-per-quarter-note MIDI time divisions are supported (SMPTE is not).");

        var events = midiFile.GetNotes()
            .Select(n => new NoteEvent(n.Time, n.Length, n.NoteNumber))
            .OrderBy(e => e.StartTicks)
            .ToList();

        var tempo = midiFile.GetTempoMap().GetTempoChanges()
            .Select(c => (Tick: c.Time, MicrosecondsPerQuarter: c.Value.MicrosecondsPerQuarterNote))
            .OrderBy(t => t.Tick)
            .ToList();

        // GetTempoChanges omits the implicit default tempo before the first explicit event; prepend
        // it so the tempo list always covers tick 0 onward.
        if (tempo.Count == 0 || tempo[0].Tick != 0)
            tempo.Insert(0, (0L, DefaultMicrosecondsPerQuarter));

        return (events, tpqn.TicksPerQuarterNote, tempo);
    }
}
