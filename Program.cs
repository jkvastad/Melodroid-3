using System.CommandLine;
using Melodroid_3.Music;
using Melodroid_3.Output;
using Spectre.Console;

namespace Melodroid_3;

class Program
{
    static int Main(string[] args)
    {
        var maxSizeOption = new Option<int>("--max-size")
        {
            Description = "Maximum allowed numerator or denominator.",
            DefaultValueFactory = _ => 24,
        };
        var maxPrimeOption = new Option<int>("--max-prime")
        {
            Description = "Largest prime allowed in numerator or denominator factorisation.",
            DefaultValueFactory = _ => 5,
        };

        var goodFractionsCommand = new Command(
            "good-fractions",
            "Enumerate coprime p/q in [1, 2) where both p and q are maxPrime-smooth and ≤ maxSize.");
        goodFractionsCommand.Add(maxSizeOption);
        goodFractionsCommand.Add(maxPrimeOption);
        goodFractionsCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);

            if (maxSize < 1)
            {
                AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]");
                return 1;
            }
            if (maxPrime < 2)
            {
                AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]");
                return 1;
            }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            FractionTableRenderer.Render(fractions);
            return 0;
        });

        var tableCommand = new Command("table", "Console-table output commands.");
        tableCommand.Add(goodFractionsCommand);

        var root = new RootCommand("Melodroid 3 — music research from first principles.");
        root.Add(tableCommand);

        return root.Parse(args).Invoke();
    }
}
