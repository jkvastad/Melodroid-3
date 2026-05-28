using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class VoicingsTableRenderer
{
    public static void Render(
        int lcm,
        int at,
        int ktet,
        IReadOnlyList<int> placementKeys,
        IReadOnlyList<Voicing> voicings,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Root").RightAligned())
            .AddColumn(new TableColumn("Voicing").LeftAligned())
            .AddColumn(new TableColumn("Intervals").LeftAligned())
            .AddColumn(new TableColumn("Span").RightAligned())
            .AddColumn(new TableColumn("Penalty").RightAligned());

        foreach (var v in voicings)
        {
            table.AddRow(
                v.Root.ToString(),
                string.Join(" ", v.Residues),
                string.Join(" ", v.Intervals),
                v.Span.ToString(),
                v.Penalty.ToString());
        }

        var placementStr = string.Join(" ", placementKeys);
        table.Caption(
            $"voicings: {lcm}@{at} · {ktet}-tet · placement {{{placementStr}}} · " +
            $"{voicings.Count} voicings (lowest-penalty per root)");
        console.Write(table);
    }
}
