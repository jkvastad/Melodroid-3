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

        var maxLcmOption = new Option<int>("--max-lcm")
        {
            Description = "Highest LCM (wave pattern length) to compute families for.",
            DefaultValueFactory = _ => 24,
        };

        var lcmFamiliesCommand = new Command(
            "lcm-families",
            "For each L ∈ [1, maxLcm], list the max-sized good-fraction subset whose denominators have LCM exactly L.");
        lcmFamiliesCommand.Add(maxSizeOption);
        lcmFamiliesCommand.Add(maxPrimeOption);
        lcmFamiliesCommand.Add(maxLcmOption);
        lcmFamiliesCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);

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
            if (maxLcm < 1)
            {
                AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]");
                return 1;
            }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            LcmFamilyTableRenderer.Render(families);
            return 0;
        });

        var tableCommand = new Command("table", "Console-table output commands.");
        tableCommand.Add(goodFractionsCommand);
        tableCommand.Add(lcmFamiliesCommand);

        var graphLcmFamiliesCommand = new Command(
            "lcm-families",
            "Emit a Mermaid graph of subset/isomorphism/renormalized-subset relationships between LCM families.");
        graphLcmFamiliesCommand.Add(maxSizeOption);
        graphLcmFamiliesCommand.Add(maxPrimeOption);
        graphLcmFamiliesCommand.Add(maxLcmOption);
        graphLcmFamiliesCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);

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
            if (maxLcm < 1)
            {
                AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]");
                return 1;
            }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var relations = FamilyRelations.Compute(families);
            var markdown = LcmFamilyGraphRenderer.Render(families, relations, maxSize, maxPrime, maxLcm);

            var outputDir = Path.Combine("output", "graphs");
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, "lcm-families.md");
            File.WriteAllText(outputPath, markdown);

            AnsiConsole.WriteLine(Path.GetFullPath(outputPath));
            return 0;
        });

        var graphCommand = new Command("graph", "Graph-output commands (Mermaid).");
        graphCommand.Add(graphLcmFamiliesCommand);

        var root = new RootCommand("Melodroid 3 — music research from first principles.");
        root.Add(tableCommand);
        root.Add(graphCommand);

        return root.Parse(args).Invoke();
    }
}
