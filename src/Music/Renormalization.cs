namespace Melodroid_3.Music;

public static class Renormalization
{
    public static Fraction OctaveNormalize(Fraction f)
    {
        var num = f.Numerator;
        var den = f.Denominator;
        while ((double)num / den < 1.0) num *= 2;
        while ((double)num / den >= 2.0) den *= 2;
        var g = Gcd(num, den);
        return new Fraction(num / g, den / g);
    }

    public static IReadOnlyList<Fraction> Renormalize(
        IReadOnlyList<Fraction> family,
        Fraction baseFraction)
    {
        var seen = new HashSet<Fraction>();
        var result = new List<Fraction>();
        foreach (var f in family)
        {
            var num = f.Numerator * baseFraction.Denominator;
            var den = f.Denominator * baseFraction.Numerator;
            var g = Gcd(num, den);
            var divided = new Fraction(num / g, den / g);
            var normalized = OctaveNormalize(divided);
            if (seen.Add(normalized)) result.Add(normalized);
        }
        result.Sort((a, b) => a.Value.CompareTo(b.Value));
        return result;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }
}
