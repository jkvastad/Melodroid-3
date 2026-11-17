using AwesomeAssertions;
using Melodroid_3.Music;
using Melodroid_3.Output;
using Spectre.Console;

namespace Melodroid_3.Tests.Output;

public class ScaleFingerprintTableRendererTests
{
    private static IReadOnlyList<LcmFamily> Families() =>
        LcmFamilies.Compute(GoodFractions.Enumerate(24, 5), 24);

    private static IReadOnlyList<FingerprintRow> Rows(int[] chord) =>
        ScaleFingerprint.ContainingChord(chord, Families(), 4, false, false, 12);

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

        ScaleFingerprintTableRenderer.Render(chord, 12, rows, nameComposites: false, console);

        var output = writer.ToString();
        output.Should().Contain("scale-fingerprint: chord={0, 4, 7}");
        output.Should().Contain($"{rows.Count} scale rows");
        output.Should().Contain("12-tet");
    }

    [Fact]
    public void Render_LabelsRowsByConstituentsByDefault()
    {
        var chord = new[] { 0, 4, 7 };
        var rows = Rows(chord);
        var (console, writer) = PlainConsole();

        ScaleFingerprintTableRenderer.Render(chord, 12, rows, nameComposites: false, console);

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

        ScaleFingerprintTableRenderer.Render(chord, 12, rows, nameComposites: true, console);

        var output = writer.ToString();
        output.Should().Contain("M@0");            // major as a named single placement
        output.Should().Contain("bb@0");           // bebop dominant composite named
        output.Should().Contain("M=major");        // legend present
    }
}
