using Melodroid_3.Music;
using Melodroid_3.Physics;
using ScottPlot;

namespace Melodroid_3.Output;

public static class LcmFamilyWaveformRenderer
{
    public static void Render(
        LcmFamily family,
        string outputPath,
        int samplesPerPeriod = 200,
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

            var scatter = plot.Add.Scatter(t, y);
            scatter.LegendText = fraction.ToString();
            scatter.MarkerSize = 0;
            scatter.LineWidth = 1.5f;
        }

        if (sharedT is not null)
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

        plot.Title($"LCM family L={family.Lcm} ({n} fractions)");
        plot.XLabel("t (reference periods)");
        plot.YLabel("amplitude");
        plot.ShowLegend();

        plot.SavePng(outputPath, width, height);
    }
}
