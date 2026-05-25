namespace Melodroid_3.Music;

public readonly record struct KeysNeededRow(double BinRadius, int? MinK);

public static class KeysNeeded
{
    public static IReadOnlyList<KeysNeededRow> Compute(
        IReadOnlyList<Fraction> goodFractions,
        double startBinRadius,
        double maxBinRadius,
        double radiusStep,
        int maxK)
    {
        var rows = new List<KeysNeededRow>();
        if (radiusStep <= 0.0 || maxBinRadius < startBinRadius) return rows;

        var stepCount = (int)Math.Floor((maxBinRadius - startBinRadius) / radiusStep) + 1;
        for (var i = 0; i < stepCount; i++)
        {
            var radius = startBinRadius + i * radiusStep;
            var minK = FindMinK(goodFractions, radius, maxK);
            rows.Add(new KeysNeededRow(radius, minK));
        }
        return rows;
    }

    public static bool IsKtetCoverage(
        IReadOnlyList<Fraction> goodFractions, int k, double binRadius)
    {
        foreach (var g in goodFractions)
        {
            if (!IsFractionCovered(g.Value, k, binRadius)) return false;
        }
        return true;
    }

    private static int? FindMinK(IReadOnlyList<Fraction> goodFractions, double radius, int maxK)
    {
        for (var k = 1; k <= maxK; k++)
        {
            if (IsKtetCoverage(goodFractions, k, radius)) return k;
        }
        return null;
    }

    private static bool IsFractionCovered(double gValue, int k, double radius)
    {
        var idealN = k * Math.Log2(gValue);
        var floorN = (int)Math.Floor(idealN);
        var ceilN = (int)Math.Ceiling(idealN);

        var nFloor = ((floorN % k) + k) % k;
        var nCeil = ((ceilN % k) + k) % k;

        if (CheckCandidate(gValue, k, nFloor, radius)) return true;
        if (nCeil != nFloor && CheckCandidate(gValue, k, nCeil, radius)) return true;
        return false;
    }

    private static bool CheckCandidate(double gValue, int k, int n, double radius)
    {
        var keyRatio = Math.Pow(2.0, (double)n / k);
        var signedRel = RatioMath.CircularSignedRelative(keyRatio, gValue);
        return Math.Abs(signedRel) <= radius;
    }
}
