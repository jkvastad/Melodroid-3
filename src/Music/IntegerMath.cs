namespace Melodroid_3.Music;

public static class IntegerMath
{
    public static int Gcd(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0) (a, b) = (b, a % b);
        return a == 0 ? 1 : a;
    }

    public static int Lcm(int a, int b) => a / Gcd(a, b) * b;
}
