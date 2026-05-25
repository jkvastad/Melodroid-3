namespace Melodroid_3.Music;

public readonly record struct KtetNearestKey(int N, double KeyRatio, double SignedRelative);

public readonly record struct KtetCutoffRow(
    int K,
    double Radius,
    Fraction LimitingFraction,
    int KeyIndex,
    double KeyRatio);

public static class KeysNeeded
{
    public static bool IsKtetCoverage(
        IReadOnlyList<Fraction> goodFractions, int k, double binRadius)
    {
        foreach (var g in goodFractions)
        {
            if (!IsFractionCovered(g.Value, k, binRadius)) return false;
        }
        return true;
    }

    public static KtetNearestKey NearestKey(double gValue, int k)
    {
        var idealN = k * Math.Log2(gValue);
        var floorN = (int)Math.Floor(idealN);
        var ceilN = (int)Math.Ceiling(idealN);

        var nFloor = ((floorN % k) + k) % k;
        var nCeil = ((ceilN % k) + k) % k;

        var ratioFloor = Math.Pow(2.0, (double)nFloor / k);
        var relFloor = RatioMath.CircularSignedRelative(ratioFloor, gValue);

        if (nCeil == nFloor)
        {
            return new KtetNearestKey(nFloor, ratioFloor, relFloor);
        }

        var ratioCeil = Math.Pow(2.0, (double)nCeil / k);
        var relCeil = RatioMath.CircularSignedRelative(ratioCeil, gValue);

        if (Math.Abs(relCeil) < Math.Abs(relFloor))
        {
            return new KtetNearestKey(nCeil, ratioCeil, relCeil);
        }
        return new KtetNearestKey(nFloor, ratioFloor, relFloor);
    }

    public static KtetCutoffRow WorstCaseForK(IReadOnlyList<Fraction> goodFractions, int k)
    {
        var worstDist = -1.0;
        var worstFraction = goodFractions[0];
        var worstN = 0;
        var worstKeyRatio = 1.0;
        foreach (var g in goodFractions)
        {
            var nk = NearestKey(g.Value, k);
            var dist = Math.Abs(nk.SignedRelative);
            if (dist > worstDist)
            {
                worstDist = dist;
                worstFraction = g;
                worstN = nk.N;
                worstKeyRatio = nk.KeyRatio;
            }
        }
        return new KtetCutoffRow(k, worstDist, worstFraction, worstN, worstKeyRatio);
    }

    public static IReadOnlyList<KtetCutoffRow> ComputeCutoffs(
        IReadOnlyList<Fraction> goodFractions, int maxK)
    {
        var rows = new List<KtetCutoffRow>();
        for (var k = 1; k <= maxK; k++)
        {
            rows.Add(WorstCaseForK(goodFractions, k));
        }
        return rows;
    }

    private static bool IsFractionCovered(double gValue, int k, double radius)
        => Math.Abs(NearestKey(gValue, k).SignedRelative) <= radius;
}
