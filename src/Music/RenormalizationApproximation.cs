namespace Melodroid_3.Music;

// One renormalized image together with the good fraction it snaps onto and the exact
// bin radius that snap costs. AlreadyGood images snap onto themselves at radius 0/1.
public readonly record struct SnappedImage(
    Fraction Image,
    Fraction Nearest,
    Fraction Radius,
    bool AlreadyGood);

public static class RenormalizationApproximation
{
    // Snap each renormalized image to its nearest good fraction; Radius is the exact
    // bin distance to that fraction (0/1 when the image is already a good fraction).
    public static IReadOnlyList<SnappedImage> Snap(
        IReadOnlyList<Fraction> images,
        IReadOnlyList<Fraction> goodFractions)
    {
        var goodSet = new HashSet<Fraction>(goodFractions);
        var result = new List<SnappedImage>(images.Count);
        foreach (var image in images)
        {
            var nearest = NearestGoodFraction(image, goodFractions, out _);
            var alreadyGood = goodSet.Contains(image);
            var radius = alreadyGood
                ? new Fraction(0, 1)
                : BinOverlaps.ExactBinDistance(image, nearest);
            result.Add(new SnappedImage(image, nearest, radius, alreadyGood));
        }
        return result;
    }

    // Largest snap radius over the images = the smallest bin radius that makes the whole
    // renormalization good. Returns 0/1 when every image is already good.
    public static Fraction RequiredRadius(IReadOnlyList<SnappedImage> snapped)
    {
        var best = new Fraction(0, 1);
        foreach (var s in snapped)
        {
            if (s.Radius.Value > best.Value) best = s.Radius;
        }
        return best;
    }

    // Nearest good fraction to an arbitrary ratio under the multiplicative bin metric
    // (RatioMath.BinDistance). Shared primitive; also used by FamilyRelations clustering.
    public static Fraction NearestGoodFraction(
        Fraction image,
        IReadOnlyList<Fraction> goodFractions,
        out double distance)
    {
        var best = goodFractions[0];
        var bestDist = RatioMath.BinDistance(image.Value, best.Value);
        for (var i = 1; i < goodFractions.Count; i++)
        {
            var d = RatioMath.BinDistance(image.Value, goodFractions[i].Value);
            if (d < bestDist)
            {
                bestDist = d;
                best = goodFractions[i];
            }
        }
        distance = bestDist;
        return best;
    }
}
