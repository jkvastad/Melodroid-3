namespace Melodroid_3.Physics;

public static class Sine
{
    public static (double[] T, double[] Y) Samples(
        double frequencyRatio,
        int durationPeriods,
        int samplesPerPeriod)
    {
        if (durationPeriods < 1) throw new ArgumentOutOfRangeException(nameof(durationPeriods));
        if (samplesPerPeriod < 1) throw new ArgumentOutOfRangeException(nameof(samplesPerPeriod));

        var count = samplesPerPeriod * durationPeriods + 1;
        var t = new double[count];
        var y = new double[count];
        var dt = 1.0 / samplesPerPeriod;
        var omega = 2 * Math.PI * frequencyRatio;

        for (var i = 0; i < count; i++)
        {
            t[i] = i * dt;
            y[i] = Math.Sin(omega * t[i]);
        }

        return (t, y);
    }
}
