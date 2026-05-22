namespace Melodroid_3.Music;

public readonly record struct BinOverlap(Fraction Lower, Fraction Upper, double Radius);

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
            var c = (b.Value - a.Value) / (b.Value + a.Value);
            result.Add(new BinOverlap(a, b, c));
        }

        var last = fractions[^1];
        var two = new Fraction(2, 1);
        var wrapC = (2.0 - last.Value) / (2.0 + last.Value);
        result.Add(new BinOverlap(last, two, wrapC));

        return result;
    }
}
