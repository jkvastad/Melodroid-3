using Melodroid_3.Music;
using Melodroid_3.Physics;
using ScottPlot;

namespace Melodroid_3.Output;

public enum PlotMode
{
    All,
    Sum,
    Constituents,
    Difference,
}

public static class LcmFamilyWaveformRenderer
{
    public static void Render(
        LcmFamily family,
        string outputPath,
        int samplesPerPeriod = 200,
        PlotMode mode = PlotMode.All,
        LcmFamily? subFamily = null,
        int width = 1400,
        int height = 800)
    {
        var plot = new Plot();

        double[]? sharedT = null;
        var sum = new double[samplesPerPeriod * family.Lcm + 1];

        foreach (var fraction in family.Fractions)
        {
            var (t, y) = Sine.Samples(fraction.Value, family.Lcm, samplesPerPeriod);
            sharedT ??= t;
            for (var i = 0; i < y.Length; i++) sum[i] += y[i];

            if (mode == PlotMode.Sum || mode == PlotMode.Difference) continue;

            var scatter = plot.Add.Scatter(t, y);
            scatter.LegendText = fraction.ToString();
            scatter.MarkerSize = 0;
            scatter.LineWidth = 1.5f;
        }

        if (sharedT is not null && mode != PlotMode.Constituents && mode != PlotMode.Difference)
        {
            var sumScatter = plot.Add.Scatter(sharedT, sum);
            sumScatter.LegendText = "sum";
            sumScatter.MarkerSize = 0;
            sumScatter.LineWidth = 3f;
            sumScatter.Color = Colors.Black;
        }

        var n = family.Fractions.Count;
        plot.Axes.SetLimitsY(-(n + 1), n + 1);
        plot.Axes.SetLimitsX(0, family.Lcm);

        var repeatLine = plot.Add.VerticalLine(family.Lcm);
        repeatLine.LinePattern = LinePattern.Dashed;
        repeatLine.Color = Colors.Gray;

        if (subFamily is { } sub)
        {
            var iterations = family.Lcm / sub.Lcm;
            for (var i = 1; i < iterations; i++)
            {
                var line = plot.Add.VerticalLine(i * sub.Lcm);
                line.LinePattern = LinePattern.Dashed;
                line.Color = Colors.Blue.WithAlpha(0.5);
            }

            var subSum = new double[samplesPerPeriod * family.Lcm + 1];
            double[]? subT = sharedT;
            foreach (var fraction in sub.Fractions)
            {
                var (t, y) = Sine.Samples(fraction.Value, family.Lcm, samplesPerPeriod);
                subT ??= t;
                for (var i = 0; i < y.Length; i++) subSum[i] += y[i];
            }

            if (mode != PlotMode.Difference)
            {
                var subScatter = plot.Add.Scatter(subT!, subSum);
                subScatter.LegendText = $"sum L={sub.Lcm} ({iterations}×)";
                subScatter.MarkerSize = 0;
                subScatter.LineWidth = 2.5f;
                subScatter.Color = Colors.Blue;
            }

            if (mode == PlotMode.Difference)
            {
                var diff = new double[sum.Length];
                for (var i = 0; i < diff.Length; i++) diff[i] = sum[i] - subSum[i];
                var diffScatter = plot.Add.Scatter(subT!, diff);
                diffScatter.LegendText = $"L={family.Lcm} − sub L={sub.Lcm}";
                diffScatter.MarkerSize = 0;
                diffScatter.LineWidth = 3f;
                diffScatter.Color = Colors.Red;
            }
        }

        var modeSuffix = mode switch
        {
            PlotMode.Sum => " — sum",
            PlotMode.Constituents => " — constituents",
            PlotMode.Difference => " — difference",
            _ => "",
        };
        var subSuffix = subFamily is null ? "" : $" + sub L={subFamily.Value.Lcm}";
        plot.Title($"LCM family L={family.Lcm} ({n} fractions){modeSuffix}{subSuffix}");
        plot.XLabel("t (reference periods)");
        plot.YLabel("amplitude");
        plot.ShowLegend();

        plot.SavePng(outputPath, width, height);
    }
}
