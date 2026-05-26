namespace Melodroid_3.Music;

public readonly record struct KeySweepRow(
    int KeyIndex,
    double KeyRatio,
    IReadOnlyList<OctaveSweepCell> Cells,
    int? PostBinLcm,
    bool FullMatch,
    bool Ambiguous,
    bool AllInputsBinned);

public static class KeySweep
{
    public static IReadOnlyList<KeySweepRow> Compute(
        IReadOnlyList<double> inputRatios,
        IReadOnlyList<Fraction> goodFractions,
        int k,
        double binRadius)
    {
        var rows = new List<KeySweepRow>();
        if (inputRatios.Count == 0 || goodFractions.Count == 0 || k < 1) return rows;

        for (var n = 0; n < k; n++)
        {
            var keyRatio = Math.Pow(2.0, (double)n / k);
            var row = OctaveSweep.ComputeRow(keyRatio, inputRatios, goodFractions, binRadius);
            rows.Add(new KeySweepRow(
                n,
                keyRatio,
                row.Cells,
                row.PostBinLcm,
                row.FullMatch,
                row.Ambiguous,
                row.AllInputsBinned));
        }

        return rows;
    }
}
