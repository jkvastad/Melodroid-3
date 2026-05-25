namespace Melodroid_3.Music;

public static class RatioMath
{
    public static double OctaveNormalize(double r)
    {
        while (r < 1.0) r *= 2.0;
        while (r >= 2.0) r *= 0.5;
        return r;
    }

    // The octave [1, 2) is cyclic — 1.0 and 2.0 identify. For v, g both in [1, 2)
    // pick the representative of v across the wrap that lies closest to g, then
    // return the signed relative offset to g. Sign follows the "v above g" → positive
    // convention used elsewhere: wrapUp (v ≈ 1, g ≈ 2) reads positive, wrapDn
    // (v ≈ 2, g ≈ 1) reads negative.
    public static double CircularSignedRelative(double v, double g)
    {
        var direct = (v - g) / g;
        var wrapUp = (2.0 * v - g) / g;
        var wrapDn = (v - 2.0 * g) / g;
        var best = direct;
        if (Math.Abs(wrapUp) < Math.Abs(best)) best = wrapUp;
        if (Math.Abs(wrapDn) < Math.Abs(best)) best = wrapDn;
        return best;
    }
}
