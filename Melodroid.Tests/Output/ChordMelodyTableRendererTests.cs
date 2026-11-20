using AwesomeAssertions;
using Melodroid_3.Music;
using Melodroid_3.Output;
using Spectre.Console;

namespace Melodroid_3.Tests.Output;

public class ChordMelodyTableRendererTests
{
    private static IReadOnlyList<LcmFamily> Families() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);

    private static IReadOnlyList<ChordMelodyRow> Rows(int[] chord) =>
        ChordMelody.ContainingChord(chord, Families(), 4, false, false, 12);

    // A plain-text (color-stripped) console over a StringWriter, wide enough that the table/caption
    // do not wrap and break substring assertions.
    private static (IAnsiConsole Console, StringWriter Out) PlainConsole()
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = 240;
        return (console, writer);
    }

    [Fact]
    public void Render_WritesCaptionWithChordAndRowCount()
    {
        var chord = new[] { 0, 4, 7 };
        var rows = Rows(chord);
        var (console, writer) = PlainConsole();

        ChordMelodyTableRenderer.Render(chord, 12, rows, nameComposites: false, console: console);

        var output = writer.ToString();
        output.Should().Contain("chord-melody: chord={0, 4, 7}");
        output.Should().Contain($"{rows.Count} scale rows");
        output.Should().Contain("12-tet");
    }

    [Fact]
    public void Render_LabelsRowsByConstituentsByDefault()
    {
        var chord = new[] { 0, 4, 7 };
        var rows = Rows(chord);
        var (console, writer) = PlainConsole();

        ChordMelodyTableRenderer.Render(chord, 12, rows, nameComposites: false, console: console);

        var output = writer.ToString();
        output.Should().Contain("24@0");    // major as its single-placement constituent label
        output.Should().Contain("·");       // composite constituent separator
        output.Should().Contain("●");       // membership bullets rendered
        output.Should().NotContain("M@0");  // no traditional names without --name-composites
    }

    [Fact]
    public void Render_NameCompositesRelabelsRowsAndEmitsLegend()
    {
        var chord = new[] { 0, 4, 7 };
        var rows = Rows(chord);
        var (console, writer) = PlainConsole();

        ChordMelodyTableRenderer.Render(chord, 12, rows, nameComposites: true, console: console);

        var output = writer.ToString();
        output.Should().Contain("bb@0");           // bebop dominant composite named
        output.Should().Contain("24@0");           // single-placement major row keeps its LCM label
        // Single placements are never renamed, so the major scale is not shown as a standalone "M@0"
        // row. The leading space distinguishes it from "hM@0" (harmonic major), whose M is preceded by h.
        output.Should().NotContain(" M@0");
        output.Should().Contain("M=major");        // legend present
    }

    [Fact]
    public void Render_FlagCollapseEmitsLegendOnlyWhenEnabled()
    {
        var chord = new[] { 0, 4, 7 };
        var rows = Rows(chord);

        var (onConsole, onWriter) = PlainConsole();
        ChordMelodyTableRenderer.Render(chord, 12, rows, nameComposites: false, flagCollapse: true, console: onConsole);
        onWriter.ToString().Should().Contain("collapses the placement under preference");

        var (offConsole, offWriter) = PlainConsole();
        ChordMelodyTableRenderer.Render(chord, 12, rows, nameComposites: false, flagCollapse: false, console: offConsole);
        offWriter.ToString().Should().NotContain("collapses the placement under preference");
    }
}
