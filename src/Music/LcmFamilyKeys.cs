namespace Melodroid_3.Music;

public readonly record struct LcmFamilyKeyRow(
    int Lcm,
    IReadOnlyList<Fraction> Fractions,
    IReadOnlyList<int> KeyIndices);

public static class LcmFamilyKeys
{
    public static IReadOnlyList<LcmFamilyKeyRow> Compute(
        IReadOnlyList<LcmFamily> families, int k)
    {
        var rows = new List<LcmFamilyKeyRow>(families.Count);
        foreach (var family in families)
        {
            var keys = family.Fractions
                .Select(f => KeysNeeded.NearestKey(f.Value, k).N)
                .ToList();
            rows.Add(new LcmFamilyKeyRow(family.Lcm, family.Fractions, keys));
        }
        return rows;
    }
}
