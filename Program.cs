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

        var modeOption = new Option<string>("--mode")
        {
            Description = "Output mode: 'full' (one node per family) or 'collapsed' (one node per iso class).",
            DefaultValueFactory = _ => "full",
        };
        modeOption.AcceptOnlyFromAmong("full", "collapsed");

        var graphLcmFamiliesCommand = new Command(
            "lcm-families",
            "Emit a Mermaid graph of subset/isomorphism/renormalized-subset relationships between LCM families.");
        graphLcmFamiliesCommand.Add(maxSizeOption);
        graphLcmFamiliesCommand.Add(maxPrimeOption);
        graphLcmFamiliesCommand.Add(maxLcmOption);
        graphLcmFamiliesCommand.Add(modeOption);
        graphLcmFamiliesCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var mode = parse.GetValue(modeOption) ?? "full";

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

            string markdown;
            string fileName;
            if (mode == "collapsed")
            {
                markdown = LcmFamilyGraphRenderer.RenderCollapsed(families, relations, maxSize, maxPrime, maxLcm);
                fileName = "lcm-families-collapsed.md";
            }
            else
            {
                markdown = LcmFamilyGraphRenderer.Render(families, relations, maxSize, maxPrime, maxLcm);
                fileName = "lcm-families.md";
            }

            var outputDir = Path.Combine("output", "graphs");
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, fileName);
            File.WriteAllText(outputPath, markdown);

            AnsiConsole.WriteLine(Path.GetFullPath(outputPath));
            return 0;
        });

        var graphCommand = new Command("graph", "Graph-output commands (Mermaid).");
        graphCommand.Add(graphLcmFamiliesCommand);

        var lcmOption = new Option<int>("--lcm")
        {
            Description = "LCM (wave pattern length) of the family to plot.",
            Required = true,
        };
        var samplesPerPeriodOption = new Option<int>("--samples-per-period")
        {
            Description = "Plot samples per reference period (smoothness).",
            DefaultValueFactory = _ => 200,
        };
        var plotModeOption = new Option<string>("--mode")
        {
            Description = "What to plot: 'all' (default), 'sum', 'constituents', or 'difference' (requires --subset-lcm).",
            DefaultValueFactory = _ => "all",
        };
        plotModeOption.AcceptOnlyFromAmong("all", "sum", "constituents", "difference");

        var subsetLcmOption = new Option<int?>("--subset-lcm")
        {
            Description = "Optional LCM K of a literal-subset family to highlight inside the parent plot. K must divide --lcm.",
            DefaultValueFactory = _ => null,
        };

        var plotLcmFamiliesCommand = new Command(
            "lcm-families",
            "Plot in-phase sine waves for the fractions of a given LCM family, with their superposition.");
        plotLcmFamiliesCommand.Add(maxSizeOption);
        plotLcmFamiliesCommand.Add(maxPrimeOption);
        plotLcmFamiliesCommand.Add(lcmOption);
        plotLcmFamiliesCommand.Add(samplesPerPeriodOption);
        plotLcmFamiliesCommand.Add(plotModeOption);
        plotLcmFamiliesCommand.Add(subsetLcmOption);
        plotLcmFamiliesCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var lcm = parse.GetValue(lcmOption);
            var samplesPerPeriod = parse.GetValue(samplesPerPeriodOption);
            var modeString = parse.GetValue(plotModeOption) ?? "all";
            var subsetLcm = parse.GetValue(subsetLcmOption);

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
            if (lcm < 1)
            {
                AnsiConsole.MarkupLine("[red]--lcm must be ≥ 1.[/]");
                return 1;
            }
            if (samplesPerPeriod < 1)
            {
                AnsiConsole.MarkupLine("[red]--samples-per-period must be ≥ 1.[/]");
                return 1;
            }

            var mode = modeString switch
            {
                "sum" => PlotMode.Sum,
                "constituents" => PlotMode.Constituents,
                "difference" => PlotMode.Difference,
                _ => PlotMode.All,
            };

            if (mode == PlotMode.Difference && subsetLcm is null)
            {
                AnsiConsole.MarkupLine("[red]--mode difference requires --subset-lcm.[/]");
                return 1;
            }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, lcm);
            var family = families.FirstOrDefault(f => f.Lcm == lcm);
            if (family.Fractions is null || family.Fractions.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No LCM family exists at L={lcm} under --max-size {maxSize} / --max-prime {maxPrime}. Try raising them.[/]");
                return 1;
            }

            LcmFamily? subFamily = null;
            if (subsetLcm is { } k)
            {
                if (k < 1)
                {
                    AnsiConsole.MarkupLine("[red]--subset-lcm must be ≥ 1.[/]");
                    return 1;
                }
                if (lcm % k != 0)
                {
                    AnsiConsole.MarkupLine($"[red]--subset-lcm {k} must divide --lcm {lcm} (literal subsets require K | N).[/]");
                    return 1;
                }
                var found = families.FirstOrDefault(f => f.Lcm == k);
                if (found.Fractions is null || found.Fractions.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[red]No LCM family exists at L={k} under --max-size {maxSize} / --max-prime {maxPrime}.[/]");
                    return 1;
                }
                subFamily = found;
            }

            var outputDir = Path.Combine("output", "plots");
            Directory.CreateDirectory(outputDir);
            var baseName = mode == PlotMode.All
                ? $"lcm-family-{lcm}"
                : $"lcm-family-{lcm}-{modeString}";
            var fileName = subFamily is null
                ? $"{baseName}.png"
                : $"{baseName}-sub{subFamily.Value.Lcm}.png";
            var outputPath = Path.Combine(outputDir, fileName);
            LcmFamilyWaveformRenderer.Render(family, outputPath, samplesPerPeriod, mode, subFamily);

            AnsiConsole.WriteLine(Path.GetFullPath(outputPath));
            return 0;
        });

        var plotCommand = new Command("plot", "Plot-output commands (PNG).");
        plotCommand.Add(plotLcmFamiliesCommand);

        var root = new RootCommand("Melodroid 3 — music research from first principles.");
        root.Add(tableCommand);
        root.Add(graphCommand);
        root.Add(plotCommand);

        return root.Parse(args).Invoke();
    }
}
