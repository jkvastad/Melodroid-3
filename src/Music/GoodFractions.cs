namespace Melodroid_3.Music;

public static class GoodFractions
{
    public static IReadOnlyList<Fraction> Enumerate(int maxSize, int maxPrime)
    {
        var safeMaxSize = Math.Max(1, maxSize);
        var safeMaxPrime = Math.Max(2, maxPrime);

        var smooth = new List<int>();
        for (var n = 1; n <= safeMaxSize; n++)
        {
            if (IsSmooth(n, safeMaxPrime)) smooth.Add(n);
        }

        var result = new List<Fraction>();
        foreach (var q in smooth)
        {
            foreach (var p in smooth)
            {
                if (p < q || p >= 2 * q) continue;
                if (Gcd(p, q) != 1) continue;
                result.Add(new Fraction(p, q));
            }
        }

        result.Sort((a, b) => a.Value.CompareTo(b.Value));
        return result;
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }

    private static bool IsSmooth(int n, int maxPrime)
    {
        for (var prime = 2; prime <= maxPrime; prime++)
        {
            if (!IsPrime(prime)) continue;
            while (n % prime == 0) n /= prime;
        }
        return n == 1;
    }

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        for (var i = 2; i * i <= n; i++)
        {
            if (n % i == 0) return false;
        }
        return true;
    }
}
