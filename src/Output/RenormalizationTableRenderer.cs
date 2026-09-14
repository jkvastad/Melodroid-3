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
            .AddColumn(new TableColumn("Fractions").LeftAligned())
            .AddColumn(new TableColumn("LCM").RightAligned());

        foreach (var (baseFrac, fractions) in rows)
        {
            var joined = string.Join(", ", fractions.Select(f => f.ToString()));
            var lcm = RatioMath.WavePatternLength(fractions);
            table.AddRow(baseFrac.ToString(), fractions.Count.ToString(), joined, lcm.ToString());
        }

        table.Caption($"lcm-{family.Lcm} · {family.Fractions.Count} members");
        console.Write(table);
    }

    // Approximate mode: each renormalization also snaps its non-good images to the nearest
    // good fraction, reporting the exact bin radius that snap requires.
    public static void RenderApproximate(
        LcmFamily family,
        IReadOnlyList<(Fraction Base, IReadOnlyList<SnappedImage> Snapped)> rows,
        IAnsiConsole? console = null,
        bool perSnapC = false,
        IReadOnlyList<IReadOnlyList<SnapFrontierPoint>>? frontiers = null)
    {
        console ??= AnsiConsole.Console;

        var table = new Table()
            .AddColumn(new TableColumn("Base").RightAligned())
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn(new TableColumn("Fractions").LeftAligned())
            .AddColumn(new TableColumn("LCM").RightAligned())
            .AddColumn(new TableColumn("→ good").LeftAligned())
            .AddColumn(new TableColumn("LCM ✓").RightAligned())
            .AddColumn(new TableColumn("c").RightAligned())
            .AddColumn(new TableColumn("c %").RightAligned());
        if (frontiers is not null) table.AddColumn(new TableColumn("LCM↔c options").LeftAligned());

        var worst = new Fraction(0, 1);
        for (var i = 0; i < rows.Count; i++)
        {
            var (baseFrac, snapped) = rows[i];
            // Non-good images are highlighted; good ones render plainly.
            var fractions = string.Join(", ", snapped.Select(s =>
                s.AlreadyGood ? s.Image.ToString() : $"[red]{s.Image}[/]"));
            // When requested, each actually-snapped (non-good) target carries the exact
            // bin radius that snap required, as fraction + %.
            var good = string.Join(", ", snapped.Select(s =>
                perSnapC && !s.AlreadyGood
                    ? $"{s.Nearest} ({s.Radius}, {s.Radius.Value.ToString("P3", CultureInfo.InvariantCulture)})"
                    : s.Nearest.ToString()));

            var required = RenormalizationApproximation.RequiredRadius(snapped);
            var isClean = required.Numerator == 0;
            if (required.Value > worst.Value) worst = required;

            var cExact = isClean ? "—" : required.ToString();
            var cPct = isClean
                ? string.Empty
                : required.Value.ToString("P3", CultureInfo.InvariantCulture);

            var exactLcm = RatioMath.WavePatternLength(snapped.Select(s => s.Image)).ToString();
            // The snapped set's WPL; equals exactLcm on clean rows, so shown only when snapping changed something.
            var snappedLcm = isClean
                ? "—"
                : RatioMath.WavePatternLength(snapped.Select(s => s.Nearest)).ToString();

            if (frontiers is null)
            {
                table.AddRow(baseFrac.ToString(), snapped.Count.ToString(), fractions, exactLcm, good, snappedLcm, cExact, cPct);
            }
            else
            {
                // Lowest-LCM (most-compressed) option first and bolded; radii fall as LCM rises.
                var points = frontiers[i];
                var frontierCell = points.Count == 0
                    ? "—"
                    : string.Join("\n", points.Select((p, idx) =>
                    {
                        var text = $"{p.Lcm} → {p.WorstRadius.Value.ToString("P2", CultureInfo.InvariantCulture)}";
                        return idx == 0 ? $"[bold]{text}[/]" : text;
                    }));
                table.AddRow(baseFrac.ToString(), snapped.Count.ToString(), fractions, exactLcm, good, snappedLcm, cExact, cPct, frontierCell);
            }
        }

        var worstText = worst.Numerator == 0
            ? "all renormalizations already good"
            : $"worst required radius {worst} ({worst.Value.ToString("P3", CultureInfo.InvariantCulture)})";
        table.Caption($"lcm-{family.Lcm} · {family.Fractions.Count} members · {worstText} · JND ≈ 0.5–1 %");
        console.Write(table);
    }
}
