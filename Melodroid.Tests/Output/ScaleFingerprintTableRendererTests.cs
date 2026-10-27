using AwesomeAssertions;
using Melodroid_3.Music;
using Melodroid_3.Output;
using Spectre.Console;

namespace Melodroid_3.Tests.Output;

public class ScaleFingerprintTableRendererTests
{
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
        var rows = NamedScales.ContainingChord(chord);
        var (console, writer) = PlainConsole();

        ScaleFingerprintTableRenderer.Render(chord, 12, rows, console);

        var output = writer.ToString();
        output.Should().Contain("scale-fingerprint: chord={0, 4, 7}");
        output.Should().Contain($"{rows.Count} scale rows");
        output.Should().Contain("6 scales · 12-tet");
    }

    [Fact]
    public void Render_LabelsEachRowByNameAndAnchor()
    {
        var chord = new[] { 0, 4, 7 };
        var rows = NamedScales.ContainingChord(chord);
        var (console, writer) = PlainConsole();

        ScaleFingerprintTableRenderer.Render(chord, 12, rows, console);

        var output = writer.ToString();
        output.Should().Contain("major @0");
        output.Should().Contain("bebop dominant @0");
        output.Should().Contain("●"); // membership bullets rendered
    }
}
