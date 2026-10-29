using System.Globalization;
using System.Text;
using Melodroid_3.Music;

namespace Melodroid_3.Output;

// Exports `analyze midi --segments` output as a Reaper region/marker CSV (import via Reaper's
// Region/Marker Manager → right-click → "Import region/marker set…"). Scale segments become regions
// (named colored bars spanning their beat range) and placement segments become markers at each
// segment's start — the two don't then fight over Reaper's single region lane. Times are wall-clock
// seconds (formatted M:SS.mmm, which Reaper's importer parses regardless of ruler mode), so segment
// beats are converted through the MIDI tempo map. Pure except Write.
public static class ReaperMarkerCsv
{
    // Placeholder name for an empty-witness segment (a chunk no label contains).
    private const string NoWitness = "(none)";

    // Wall-clock seconds at a given tick under a piecewise-constant tempo map (sorted
    // (tick, µs-per-quarter) pairs covering tick 0 onward). Pure — the independent oracle for tests.
    public static double TicksToSeconds(
        long tick, int ticksPerQuarter,
        IReadOnlyList<(long Tick, long MicrosecondsPerQuarter)> tempo)
    {
        var seconds = 0.0;
        for (var i = 0; i < tempo.Count; i++)
        {
            var segStart = tempo[i].Tick;
            if (segStart >= tick) break;
            var segEnd = i + 1 < tempo.Count ? Math.Min(tempo[i + 1].Tick, tick) : tick;
            var secondsPerTick = tempo[i].MicrosecondsPerQuarter / 1_000_000.0 / ticksPerQuarter;
            seconds += (segEnd - segStart) * secondsPerTick;
        }
        return seconds;
    }

    // The full CSV text: scale segments as region rows, placement segments as start-marker rows.
    public static string Build(
        IReadOnlyList<LabelSegment> scaleSegments,
        IReadOnlyList<LabelSegment> placementSegments,
        int ticksPerQuarter,
        IReadOnlyList<(long Tick, long MicrosecondsPerQuarter)> tempo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#,Name,Start,End,Length,Color");

        var n = 1;
        foreach (var seg in scaleSegments)
        {
            var start = Seconds(seg.StartBeat);
            var end = Seconds(seg.EndBeat);
            sb.AppendLine(string.Join(',',
                $"R{n++}", Escape(Name(seg)), Time(start), Time(end), Time(end - start), ""));
        }

        n = 1;
        foreach (var seg in placementSegments)
        {
            var start = Seconds(seg.StartBeat);
            sb.AppendLine(string.Join(',', $"M{n++}", Escape(Name(seg)), Time(start), "", "", ""));
        }

        return sb.ToString();

        double Seconds(double beat) =>
            TicksToSeconds((long)Math.Round(beat * ticksPerQuarter), ticksPerQuarter, tempo);
    }

    public static void Write(string path, string csv)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, csv);
    }

    private static string Name(LabelSegment seg) =>
        seg.Witnesses.Count == 0 ? NoWitness : string.Join(' ', seg.Witnesses);

    // M:SS.mmm — the unambiguous time format Reaper's CSV importer always reads as wall-clock.
    private static string Time(double seconds)
    {
        var minutes = (int)(seconds / 60);
        var rest = seconds - minutes * 60;
        return $"{minutes}:{rest.ToString("00.000", CultureInfo.InvariantCulture)}";
    }

    // Minimal CSV field escaping: quote and double any embedded quote when the field contains a
    // comma, quote, or newline.
    private static string Escape(string field)
    {
        if (field.IndexOfAny([',', '"', '\n', '\r']) < 0) return field;
        return '"' + field.Replace("\"", "\"\"") + '"';
    }
}
