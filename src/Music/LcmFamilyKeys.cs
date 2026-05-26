namespace Melodroid_3.Music;

public readonly record struct LcmFamilyKeyRow(
    int Lcm,
    IReadOnlyList<Fraction> Fractions,
    IReadOnlyList<int> KeyIndices);

public readonly record struct LcmFamilyKeyCollapsedRow(
    int RepresentativeLcm,
    IReadOnlyList<int> ClassLcms,
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

    public static IReadOnlyList<LcmFamilyKeyCollapsedRow> ComputeCollapsed(
        IReadOnlyList<LcmFamily> families,
        IReadOnlyList<FamilyRelation> relations,
        int k)
    {
        var classes = FamilyRelations.BuildIsoClasses(families, relations);
        var byLcm = families.ToDictionary(f => f.Lcm);

        var rows = new List<LcmFamilyKeyCollapsedRow>(classes.Count);
        foreach (var cls in classes)
        {
            var representative = byLcm[cls[0]];
            var keys = representative.Fractions
                .Select(f => KeysNeeded.NearestKey(f.Value, k).N)
                .ToList();
            rows.Add(new LcmFamilyKeyCollapsedRow(representative.Lcm, cls, representative.Fractions, keys));
        }
        return rows;
    }
}
