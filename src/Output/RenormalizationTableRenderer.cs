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
        IReadOnlyList<IReadOnlyList<SnapFrontierPoint>>? frontiers = null,
        IReadOnlyList<PlacementImage>? placements = null)
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
        if (placements is not null) table.AddColumn(new TableColumn("Placement").LeftAligned());

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

            var cells = new List<string> { baseFrac.ToString(), snapped.Count.ToString(), fractions, exactLcm, good, snappedLcm, cExact, cPct };

            if (frontiers is not null)
            {
                // Lowest-LCM (most-compressed) option first and bolded; radii fall as LCM rises.
                var points = frontiers[i];
                cells.Add(points.Count == 0
                    ? "—"
                    : string.Join("\n", points.Select((p, idx) =>
                    {
                        var text = $"{p.Lcm} → {p.WorstRadius.Value.ToString("P2", CultureInfo.InvariantCulture)}";
                        return idx == 0 ? $"[bold]{text}[/]" : text;
                    })));
            }

            if (placements is not null)
            {
                // Good rows re-root the snapped set on the base's key: = for an exact isomorphism,
                // ⊆ for a renormalized proper subset. Rows that balloon or exceed the comma show —.
                var p = placements[i];
                cells.Add(p.Good ? $"{(p.Exact ? "=" : "⊆")} {p.Lcm}@{p.At}" : "—");
            }

            table.AddRow(cells.ToArray());
        }

        var worstText = worst.Numerator == 0
            ? "all renormalizations already good"
            : $"worst required radius {worst} ({worst.Value.ToString("P3", CultureInfo.InvariantCulture)})";
        table.Caption($"lcm-{family.Lcm} · {family.Fractions.Count} members · {worstText} · JND ≈ 0.5–1 %");
        console.Write(table);
    }
}
