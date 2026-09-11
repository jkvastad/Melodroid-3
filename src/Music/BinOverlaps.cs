namespace Melodroid_3.Music;

public readonly record struct BinOverlap(Fraction Lower, Fraction Upper, Fraction Radius);

public static class BinOverlaps
{
    public static IReadOnlyList<BinOverlap> Compute(IReadOnlyList<Fraction> fractions)
    {
        var result = new List<BinOverlap>();
        if (fractions.Count == 0) return result;

        for (var i = 0; i < fractions.Count - 1; i++)
        {
            var a = fractions[i];
            var b = fractions[i + 1];
            result.Add(new BinOverlap(a, b, ExactBinDistance(a, b)));
        }

        var last = fractions[^1];
        var two = new Fraction(2, 1);
        result.Add(new BinOverlap(last, two, ExactBinDistance(last, two)));

        return result;
    }

    // c = |b - a| / (b + a), the relative bin distance between two fractions,
    // expressed exactly as a reduced fraction. Order-independent.
    public static Fraction ExactBinDistance(Fraction a, Fraction b)
    {
        var num = Math.Abs(b.Numerator * a.Denominator - a.Numerator * b.Denominator);
        var den = b.Numerator * a.Denominator + a.Numerator * b.Denominator;
        var g = IntegerMath.Gcd(num, den);
        return new Fraction(num / g, den / g);
    }
}
