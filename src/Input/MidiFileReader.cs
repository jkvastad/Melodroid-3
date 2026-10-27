using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using NoteEvent = Melodroid_3.Music.NoteEvent;

namespace Melodroid_3.Input;

// The single place DryWetMidi is referenced. Reads a MIDI file into the pure NoteEvent list that
// SongAnalysis consumes, along with the file's ticks-per-quarter-note (the beat unit). Note on/off
// pairing, tempo, and channel/track structure are flattened away here — SongAnalysis works only on
// (startTicks, lengthTicks, noteNumber) triples folded mod 12, so this first cut ignores everything
// a chunked pitch-class view doesn't need.
public static class MidiFileReader
{
    public static (IReadOnlyList<NoteEvent> Events, int TicksPerQuarter) Read(string path)
    {
        var midiFile = MidiFile.Read(path);

        if (midiFile.TimeDivision is not TicksPerQuarterNoteTimeDivision tpqn)
            throw new NotSupportedException(
                "Only ticks-per-quarter-note MIDI time divisions are supported (SMPTE is not).");

        var events = midiFile.GetNotes()
            .Select(n => new NoteEvent(n.Time, n.Length, n.NoteNumber))
            .OrderBy(e => e.StartTicks)
            .ToList();

        return (events, tpqn.TicksPerQuarterNote);
    }
}
