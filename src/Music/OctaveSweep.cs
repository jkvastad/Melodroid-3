namespace Melodroid_3.Music;

public readonly record struct OctaveSweepMatch(
    Fraction Fraction,
    double SignedPctDistance);

public readonly record struct OctaveSweepCell(
    Fraction GoodFraction,
    double NormalizedRatio,
    double SignedPctDistance,
    bool Ambiguous,
    IReadOnlyList<OctaveSweepMatch> Matches);

public readonly record struct OctaveSweepRow(
    double ReferenceRatio,
    IReadOnlyList<OctaveSweepCell> Cells,
    int? PostBinLcm,
    bool FullMatch,
    bool Ambiguous,
    bool AllInputsBinned);

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
        var noMisses = true;
        var foldedLcm = 1;
        var anyBinned = false;

        for (var i = 0; i < inputRatios.Count; i++)
        {
            var normalized = RatioMath.OctaveNormalize(inputRatios[i] / reference);

            var matches = new List<OctaveSweepMatch>();

            foreach (var gf in goodFractions)
            {
                var signedRel = RatioMath.CircularSignedRelative(normalized, gf.Value);
                if (Math.Abs(signedRel) <= binRadius)
                {
                    matches.Add(new OctaveSweepMatch(gf, signedRel * 100.0));
                }
            }

            if (matches.Count == 0)
            {
                cells[i] = new OctaveSweepCell(default, normalized, double.NaN, false, matches);
                allBinned = false;
                noMisses = false;
            }
            else if (matches.Count == 1)
            {
                var primary = matches[0];
                cells[i] = new OctaveSweepCell(primary.Fraction, normalized, primary.SignedPctDistance, false, matches);
                anyBinned = true;
                foldedLcm = IntegerMath.Lcm(foldedLcm, primary.Fraction.Denominator);
            }
            else
            {
                var primary = matches[0];
                cells[i] = new OctaveSweepCell(primary.Fraction, normalized, primary.SignedPctDistance, true, matches);
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

        return new OctaveSweepRow(reference, cells, postBinLcm, fullMatch, rowAmbiguous, noMisses);
    }

    // A block is a maximal contiguous run of rows where every input ratio fell
    // into at least one good-fraction bin (AllInputsBinned) and that share the same
    // per-input-position tuple of matched good fractions. Ambiguous-overlap rows
    // are included alongside strict full matches — their cell signatures use the
    // first matched fraction, which keeps grouping well-defined. Each block
    // contributes one centered row: the index minimising max |SignedPctDistance|
    // across cells, ties broken by lowest index.
    public static IReadOnlyCollection<int> IdentifyCenteredFullMatches(
        IReadOnlyList<OctaveSweepRow> rows)
    {
        var centered = new HashSet<int>();
        var blockStart = -1;
        var bestIndex = -1;
        var bestMaxAbs = double.PositiveInfinity;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (!row.AllInputsBinned)
            {
                if (blockStart >= 0)
                {
                    centered.Add(bestIndex);
                    blockStart = -1;
                }
                continue;
            }

            if (blockStart >= 0 && !SameSignature(rows[blockStart].Cells, row.Cells))
            {
                centered.Add(bestIndex);
                blockStart = -1;
            }

            if (blockStart < 0)
            {
                blockStart = i;
                bestIndex = i;
                bestMaxAbs = MaxAbsDistance(row.Cells);
            }
            else
            {
                var maxAbs = MaxAbsDistance(row.Cells);
                if (maxAbs < bestMaxAbs)
                {
                    bestMaxAbs = maxAbs;
                    bestIndex = i;
                }
            }
        }

        if (blockStart >= 0) centered.Add(bestIndex);
        return centered;
    }

    private static bool SameSignature(
        IReadOnlyList<OctaveSweepCell> a,
        IReadOnlyList<OctaveSweepCell> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].GoodFraction != b[i].GoodFraction) return false;
        }
        return true;
    }

    private static double MaxAbsDistance(IReadOnlyList<OctaveSweepCell> cells)
    {
        var max = 0.0;
        for (var i = 0; i < cells.Count; i++)
        {
            var abs = Math.Abs(cells[i].SignedPctDistance);
            if (abs > max) max = abs;
        }
        return max;
    }

}
