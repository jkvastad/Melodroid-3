namespace Melodroid_3.Music;

public readonly record struct LcmFamily(int Lcm, IReadOnlyList<Fraction> Fractions);

public static class LcmFamilies
{
    public static IReadOnlyList<LcmFamily> Compute(
        IReadOnlyList<Fraction> goodFractions,
        int maxLcm)
    {
        var result = new List<LcmFamily>();
        if (maxLcm < 1) return result;

        for (var l = 1; l <= maxLcm; l++)
        {
            var members = new List<Fraction>();
            var foldedLcm = 1;
            foreach (var f in goodFractions)
            {
                if (l % f.Denominator != 0) continue;
                members.Add(f);
                foldedLcm = IntegerMath.Lcm(foldedLcm, f.Denominator);
            }

            if (foldedLcm == l) result.Add(new LcmFamily(l, members));
        }

        return result;
    }
}
