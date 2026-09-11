using System.Globalization;
using Melodroid_3.Music;
using Spectre.Console;

namespace Melodroid_3.Output;

public static class RenormalizationTableRenderer
{
    public static void Render(
        LcmFamily family,
        IReadOnlyList<(Fraction Base, IReadOnlyList<Fraction> Fractions)> rows,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Base").RightAligned())
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn(new TableColumn("Fractions").LeftAligned());

        foreach (var (baseFrac, fractions) in rows)
        {
            var joined = string.Join(", ", fractions.Select(f => f.ToString()));
            table.AddRow(baseFrac.ToString(), fractions.Count.ToString(), joined);
        }

        table.Caption($"lcm-{family.Lcm} · {family.Fractions.Count} members");
        console.Write(table);
    }

    // Approximate mode: each renormalization also snaps its non-good images to the nearest
    // good fraction, reporting the exact bin radius that snap requires.
    public static void RenderApproximate(
        LcmFamily family,
        IReadOnlyList<(Fraction Base, IReadOnlyList<SnappedImage> Snapped)> rows,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Base").RightAligned())
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn(new TableColumn("Fractions").LeftAligned())
            .AddColumn(new TableColumn("→ good").LeftAligned())
            .AddColumn(new TableColumn("c").RightAligned())
            .AddColumn(new TableColumn("c %").RightAligned());

        var worst = new Fraction(0, 1);
        foreach (var (baseFrac, snapped) in rows)
        {
            // Non-good images are highlighted; good ones render plainly.
            var fractions = string.Join(", ", snapped.Select(s =>
                s.AlreadyGood ? s.Image.ToString() : $"[red]{s.Image}[/]"));
            var good = string.Join(", ", snapped.Select(s => s.Nearest.ToString()));

            var required = RenormalizationApproximation.RequiredRadius(snapped);
            var isClean = required.Numerator == 0;
            if (required.Value > worst.Value) worst = required;

            var cExact = isClean ? "—" : required.ToString();
            var cPct = isClean
                ? string.Empty
                : required.Value.ToString("P3", CultureInfo.InvariantCulture);

            table.AddRow(baseFrac.ToString(), snapped.Count.ToString(), fractions, good, cExact, cPct);
        }

        var worstText = worst.Numerator == 0
            ? "all renormalizations already good"
            : $"worst required radius {worst} ({worst.Value.ToString("P3", CultureInfo.InvariantCulture)})";
        table.Caption($"lcm-{family.Lcm} · {family.Fractions.Count} members · {worstText} · JND ≈ 0.5–1 %");
        console.Write(table);
    }
}
