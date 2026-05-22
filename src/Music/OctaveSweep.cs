namespace Melodroid_3.Music;

public readonly record struct OctaveSweepCell(
    Fraction GoodFraction,
    double NormalizedRatio,
    double SignedPctDistance,
    bool Ambiguous);

public readonly record struct OctaveSweepRow(
    double ReferenceRatio,
    IReadOnlyList<OctaveSweepCell> Cells,
    int? PostBinLcm,
    bool FullMatch,
    bool Ambiguous);

public static class OctaveSweep
{
    public static IReadOnlyList<OctaveSweepRow> Compute(
        IReadOnlyList<double> inputRatios,
        IReadOnlyList<Fraction> goodFractions,
        double sweepStep,
        double binRadius)
    {
        var rows = new List<OctaveSweepRow>();
        if (inputRatios.Count == 0 || goodFractions.Count == 0) return rows;

        var stepCount = (int)Math.Ceiling((2.0 - 1.0) / sweepStep);
        for (var i = 0; i < stepCount; i++)
        {
            var reference = 1.0 + i * sweepStep;
            if (reference >= 2.0) break;
            rows.Add(ComputeRow(reference, inputRatios, goodFractions, binRadius));
        }

        return rows;
    }

    private static OctaveSweepRow ComputeRow(
        double reference,
        IReadOnlyList<double> inputRatios,
        IReadOnlyList<Fraction> goodFractions,
        double binRadius)
    {
        var cells = new OctaveSweepCell[inputRatios.Count];
        var rowAmbiguous = false;
        var allBinned = true;
        var foldedLcm = 1;
        var anyBinned = false;

        for (var i = 0; i < inputRatios.Count; i++)
        {
            var normalized = OctaveNormalize(inputRatios[i] / reference);

            Fraction matched = default;
            var matchedDistance = double.NaN;
            var matchCount = 0;

            foreach (var gf in goodFractions)
            {
                var signedRel = (normalized - gf.Value) / gf.Value;
                if (Math.Abs(signedRel) <= binRadius)
                {
                    if (matchCount == 0)
                    {
                        matched = gf;
                        matchedDistance = signedRel * 100.0;
                    }
                    matchCount++;
                }
            }

            if (matchCount == 0)
            {
                cells[i] = new OctaveSweepCell(default, normalized, double.NaN, false);
                allBinned = false;
            }
            else if (matchCount == 1)
            {
                cells[i] = new OctaveSweepCell(matched, normalized, matchedDistance, false);
                anyBinned = true;
                foldedLcm = IntegerMath.Lcm(foldedLcm, matched.Denominator);
            }
            else
            {
                cells[i] = new OctaveSweepCell(matched, normalized, matchedDistance, true);
                rowAmbiguous = true;
                allBinned = false;
            }
        }

        int? postBinLcm;
        bool fullMatch;
        if (rowAmbiguous)
        {
            postBinLcm = null;
            fullMatch = false;
        }
        else if (allBinned)
        {
            postBinLcm = foldedLcm;
            fullMatch = true;
        }
        else
        {
            postBinLcm = anyBinned ? foldedLcm : null;
            fullMatch = false;
        }

        return new OctaveSweepRow(reference, cells, postBinLcm, fullMatch, rowAmbiguous);
    }

    private static double OctaveNormalize(double r)
    {
        while (r < 1.0) r *= 2.0;
        while (r >= 2.0) r *= 0.5;
        return r;
    }
}
