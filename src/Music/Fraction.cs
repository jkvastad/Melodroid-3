namespace Melodroid_3.Music;

public readonly record struct Fraction(int Numerator, int Denominator)
{
    public double Value => (double)Numerator / Denominator;
    public override string ToString() => $"{Numerator}/{Denominator}";
}
