namespace Melodroid_3.Music;

// One renormalized image together with the good fraction it snaps onto and the exact
// bin radius that snap costs. AlreadyGood images snap onto themselves at radius 0/1.
public readonly record struct SnappedImage(
    Fraction Image,
    Fraction Nearest,
    Fraction Radius,
    bool AlreadyGood);

// One point on a row's LCM↔radius tradeoff: to hold the snapped set's wave-pattern
// length down to Lcm, each image must snap within WorstRadius (the largest per-image snap).
public readonly record struct SnapFrontierPoint(
    int Lcm,
    Fraction WorstRadius,
    IReadOnlyList<Fraction> Snapped);

// A renormalization row re-expressed as a k-tet placement: the snapped set, re-rooted on the base
// fraction's key At, is family Lcm placed there. Good is true only when the snap reaches a good LCM
// family (Lcm ≤ maxLcm) entirely within the syntonic comma (required radius ≤ 1/161). Exact
// distinguishes an exact isomorphism (= Lcm@At) from a renormalized proper subset (⊆ Lcm@At).
public readonly record struct PlacementImage(bool Good, int Lcm, int At, bool Exact);

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

    // Re-express one snapped renormalization as a k-tet placement. The base fraction becomes the new
    // 1/1, sitting at key At = NearestKey(base); the snapped set is then family L' (= its wave-pattern
    // length) anchored on At. Good requires L' to be a real family within maxLcm and the whole snap to
    // stay within the syntonic comma (1/161); Exact is true when the snapped set fills the whole target
    // family (an isomorphism) rather than a proper subset. `families` supplies the target family sizes.
    public static PlacementImage AsPlacement(
        IReadOnlyList<SnappedImage> snapped,
        Fraction baseFraction,
        int ktet,
        int maxLcm,
        IReadOnlyList<LcmFamily> families)
    {
        var lcm = RatioMath.WavePatternLength(snapped.Select(s => s.Nearest));
        var required = RequiredRadius(snapped);
        // Exact fraction comparison against the syntonic comma 1/161 (a/b ≤ 1/161 ⇔ 161·a ≤ b),
        // avoiding float boundary error on the 27/16 → 5/3 case that lands exactly on 1/161.
        var withinComma = 161L * required.Numerator <= required.Denominator;
        var good = lcm <= maxLcm && withinComma;

        var at = KeysNeeded.NearestKey(baseFraction.Value, ktet).N;

        var target = families.FirstOrDefault(f => f.Lcm == lcm);
        var distinctCount = snapped.Select(s => s.Nearest).Distinct().Count();
        var exact = target.Fractions is not null && distinctCount == target.Fractions.Count;

        return new PlacementImage(good, lcm, at, exact);
    }

    // The LCM↔radius tradeoff for one renormalization. Instead of snapping each image to its
    // nearest good fraction (which lets denominators diverge and the LCM balloon), this searches,
    // for every LCM cap L in 1..maxLcm, the cheapest snap whose result's wave-pattern length
    // divides L: each image snaps to the nearest good fraction whose denominator divides L, staying
    // within maxC. Deliberately accepting a worse per-image snap can share more denominator factors
    // and so hold the overall LCM low. Returns the Pareto frontier — smaller LCM costs a larger
    // radius — sorted by LCM ascending.
    public static IReadOnlyList<SnapFrontierPoint> SnapFrontier(
        IReadOnlyList<Fraction> images,
        IReadOnlyList<Fraction> goodFractions,
        int maxLcm,
        double maxC)
    {
        var goodSet = new HashSet<Fraction>(goodFractions);

        // Best (min WorstRadius) feasible assignment found for each achievable actual LCM.
        var bestByLcm = new Dictionary<int, SnapFrontierPoint>();

        for (var l = 1; l <= maxLcm; l++)
        {
            var chosen = new List<Fraction>(images.Count);
            var worst = new Fraction(0, 1);
            var feasible = true;

            foreach (var image in images)
            {
                Fraction? best = null;
                var bestDist = double.PositiveInfinity;
                foreach (var g in goodFractions)
                {
                    if (l % g.Denominator != 0) continue;
                    var d = RatioMath.BinDistance(image.Value, g.Value);
                    if (d > maxC) continue;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = g;
                    }
                }

                if (best is null)
                {
                    feasible = false;
                    break;
                }

                var target = best.Value;
                var radius = goodSet.Contains(image) && image.Equals(target)
                    ? new Fraction(0, 1)
                    : BinOverlaps.ExactBinDistance(image, target);
                if (radius.Value > worst.Value) worst = radius;
                chosen.Add(target);
            }

            if (!feasible) continue;

            var actualLcm = RatioMath.WavePatternLength(chosen);
            if (!bestByLcm.TryGetValue(actualLcm, out var existing)
                || worst.Value < existing.WorstRadius.Value)
            {
                bestByLcm[actualLcm] = new SnapFrontierPoint(actualLcm, worst, chosen);
            }
        }

        // Pareto reduction: ascending LCM, keep only points whose radius strictly improves.
        var frontier = new List<SnapFrontierPoint>();
        var bestRadius = double.PositiveInfinity;
        foreach (var point in bestByLcm.Values.OrderBy(p => p.Lcm))
        {
            if (point.WorstRadius.Value < bestRadius)
            {
                frontier.Add(point);
                bestRadius = point.WorstRadius.Value;
            }
        }
        return frontier;
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
