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

        var binOverlapsCommand = new Command(
            "bin-overlaps",
            "For each adjacent pair of good fractions (with octave-wrap to 2/1), print the bin radius c at which their JND clusters first overlap.");
        binOverlapsCommand.Add(maxSizeOption);
        binOverlapsCommand.Add(maxPrimeOption);
        binOverlapsCommand.SetAction(parse =>
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
            var overlaps = BinOverlaps.Compute(fractions);
            BinOverlapTableRenderer.Render(overlaps);
            return 0;
        });

        var ratiosOption = new Option<double[]>("--ratios")
        {
            Description = "Input ratios on [1, 2), space-separated (e.g. --ratios 1.0 1.25 1.5).",
            Required = true,
            AllowMultipleArgumentsPerToken = true,
        };
        var sweepStepOption = new Option<double>("--sweep-step")
        {
            Description = "Unitless ratio increment for the reference sweep across [1, 2).",
            DefaultValueFactory = _ => 0.001,
        };
        var binRadiusOption = new Option<double>("--bin-radius")
        {
            Description = "Bin radius as a decimal ratio (default ≈ 1/161, the unambiguous threshold).",
            DefaultValueFactory = _ => 1.0 / 161.0,
        };
        var onlyFullMatchesOption = new Option<bool>("--only-full-matches")
        {
            Description = "Show only rows where every input ratio fell into at least one good-fraction bin. Includes ambiguous-overlap rows (highlighted yellow, marked ?) — they are full matches with an undefined LCM.",
            DefaultValueFactory = _ => false,
        };
        var onlyCenteredFullMatchesOption = new Option<bool>("--only-centered-full-matches")
        {
            Description = "Show only the centered (smallest worst-cell |distance|) step of each contiguous all-binned block. Ambiguous-overlap rows participate in blocks alongside strict full matches. Takes precedence over --only-full-matches.",
            DefaultValueFactory = _ => false,
        };

        var octaveSweepCommand = new Command(
            "octave-sweep",
            "Sweep a reference ratio across [1, 2) and bin renormalized input ratios against good fractions; strict full matches are highlighted green, ambiguous-overlap rows yellow. Use --only-full-matches to keep rows where every input was binned (strict + ambiguous), or --only-centered-full-matches for one row per contiguous all-binned block.");
        octaveSweepCommand.Add(maxSizeOption);
        octaveSweepCommand.Add(maxPrimeOption);
        octaveSweepCommand.Add(ratiosOption);
        octaveSweepCommand.Add(sweepStepOption);
        octaveSweepCommand.Add(binRadiusOption);
        octaveSweepCommand.Add(onlyFullMatchesOption);
        octaveSweepCommand.Add(onlyCenteredFullMatchesOption);
        octaveSweepCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var ratios = parse.GetValue(ratiosOption) ?? Array.Empty<double>();
            var sweepStep = parse.GetValue(sweepStepOption);
            var binRadius = parse.GetValue(binRadiusOption);
            var onlyFullMatches = parse.GetValue(onlyFullMatchesOption);
            var onlyCenteredFullMatches = parse.GetValue(onlyCenteredFullMatchesOption);

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
            if (ratios.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]--ratios must contain at least one value.[/]");
                return 1;
            }
            foreach (var r in ratios)
            {
                if (!(r >= 1.0 && r < 2.0))
                {
                    AnsiConsole.MarkupLine($"[red]--ratios value {r} is outside [[1, 2).[/]");
                    return 1;
                }
            }
            if (!(sweepStep > 0.0 && sweepStep < 1.0))
            {
                AnsiConsole.MarkupLine("[red]--sweep-step must be in (0, 1).[/]");
                return 1;
            }
            if (!(binRadius > 0.0 && binRadius < 1.0))
            {
                AnsiConsole.MarkupLine("[red]--bin-radius must be in (0, 1).[/]");
                return 1;
            }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var rows = OctaveSweep.Compute(ratios, fractions, sweepStep, binRadius);
            OctaveSweepTableRenderer.Render(rows, fractions, ratios, binRadius, onlyFullMatches, onlyCenteredFullMatches);
            return 0;
        });

        var cutoffsMaxKOption = new Option<int>("--max-k")
        {
            Description = "Largest k to list. Each row is the worst-case multiplicative error c_k for that k-tet keyboard against the good fractions.",
            DefaultValueFactory = _ => 50,
        };
        var onlyStrictlyImprovingOption = new Option<bool>("--only-strictly-improving")
        {
            Description = "Show only rows where c_k strictly improves over every smaller k (the green-highlighted rows).",
            DefaultValueFactory = _ => false,
        };

        var ktetCutoffsCommand = new Command(
            "ktet-cutoffs",
            "For each k in [1, --max-k], report the exact bin radius c_k at which k-tet first covers every good fraction, the limiting good fraction g_k* (argmax of distance), and the nearest k-tet key to g_k*. Green rows mark active k's — those where c_k strictly improves over every smaller k. Use --only-strictly-improving to keep only those rows.");
        ktetCutoffsCommand.Add(maxSizeOption);
        ktetCutoffsCommand.Add(maxPrimeOption);
        ktetCutoffsCommand.Add(cutoffsMaxKOption);
        ktetCutoffsCommand.Add(onlyStrictlyImprovingOption);
        ktetCutoffsCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxK = parse.GetValue(cutoffsMaxKOption);
            var onlyStrictlyImproving = parse.GetValue(onlyStrictlyImprovingOption);

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
            if (maxK < 1)
            {
                AnsiConsole.MarkupLine("[red]--max-k must be ≥ 1.[/]");
                return 1;
            }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            if (fractions.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No good fractions under --max-size {maxSize} --max-prime {maxPrime}.[/]");
                return 1;
            }

            var rows = KeysNeeded.ComputeCutoffs(fractions, maxK);
            KtetCutoffsTableRenderer.Render(rows, maxSize, maxPrime, maxK, onlyStrictlyImproving);
            return 0;
        });

        var ktetOption = new Option<int>("--ktet")
        {
            Description = "Number of equally-tempered keys per octave. The sweep emits one row per key (n = 0..k-1). Default 12.",
            DefaultValueFactory = _ => 12,
        };
        var keySweepBinRadiusOption = new Option<double?>("--bin-radius")
        {
            Description = "Optional override. Default: c_k, the exact worst-case k-tet covering radius for the chosen --ktet (every good fraction has a binnable key at this radius).",
        };
        var keySweepRatiosOption = new Option<double[]>("--ratios")
        {
            Description = "Input ratios on [1, 2), space-separated. Combined with --keys if both are given. At least one of --keys / --ratios is required.",
            AllowMultipleArgumentsPerToken = true,
        };
        var keySweepKeysOption = new Option<int[]>("--keys")
        {
            Description = "Key indices on a --ktet keyboard (0 to k-1), space-separated. Each key i is converted to ratio 2^(i/k). Combined with --ratios if both are given. At least one of --keys / --ratios is required.",
            AllowMultipleArgumentsPerToken = true,
        };

        var keySweepCommand = new Command(
            "key-sweep",
            "Sweep through the k keys of a --ktet equal-tempered system, treating each key as the reference, and bin renormalized input ratios against good fractions. Default --bin-radius is the c_k cutoff so every good fraction has a binnable key by construction.");
        keySweepCommand.Add(maxSizeOption);
        keySweepCommand.Add(maxPrimeOption);
        keySweepCommand.Add(keySweepRatiosOption);
        keySweepCommand.Add(keySweepKeysOption);
        keySweepCommand.Add(ktetOption);
        keySweepCommand.Add(keySweepBinRadiusOption);
        keySweepCommand.Add(onlyFullMatchesOption);
        keySweepCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var ratios = parse.GetValue(keySweepRatiosOption) ?? Array.Empty<double>();
            var keys = parse.GetValue(keySweepKeysOption) ?? Array.Empty<int>();
            var k = parse.GetValue(ktetOption);
            var binRadiusOverride = parse.GetValue(keySweepBinRadiusOption);
            var onlyFullMatches = parse.GetValue(onlyFullMatchesOption);

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
            if (k < 1)
            {
                AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]");
                return 1;
            }
            if (ratios.Length == 0 && keys.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]must provide at least one of --keys or --ratios.[/]");
                return 1;
            }
            foreach (var r in ratios)
            {
                if (!(r >= 1.0 && r < 2.0))
                {
                    AnsiConsole.MarkupLine($"[red]--ratios value {r} is outside [[1, 2).[/]");
                    return 1;
                }
            }
            foreach (var key in keys)
            {
                if (key < 0 || key >= k)
                {
                    AnsiConsole.MarkupLine($"[red]--keys value {key} is outside [[0, {k - 1}]].[/]");
                    return 1;
                }
            }
            if (binRadiusOverride is double overrideValue && !(overrideValue > 0.0 && overrideValue < 1.0))
            {
                AnsiConsole.MarkupLine("[red]--bin-radius must be in (0, 1).[/]");
                return 1;
            }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            if (fractions.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No good fractions under --max-size {maxSize} --max-prime {maxPrime}.[/]");
                return 1;
            }

            var combinedRatios = ratios
                .Concat(keys.Select(i => Math.Pow(2.0, (double)i / k)))
                .ToArray();
            var effectiveRadius = binRadiusOverride ?? KeysNeeded.WorstCaseForK(fractions, k).Radius;
            var rows = KeySweep.Compute(combinedRatios, fractions, k, effectiveRadius);
            KeySweepTableRenderer.Render(rows, fractions, keys, ratios, k, effectiveRadius, onlyFullMatches);
            return 0;
        });

        var placementLcmOption = new Option<int[]>("--lcm")
        {
            Description = "LCM(s) (wave pattern length) of the families to place — one row per value, in the order given.",
            Required = true,
            AllowMultipleArgumentsPerToken = true,
        };
        var placementAtOption = new Option<int>("--at")
        {
            Description = "Key index on a --ktet keyboard at which to anchor the family (must be in [0, ktet-1]).",
            Required = true,
        };

        var placementCommand = new Command(
            "placement",
            "Map the fractions of one LCM family to k-tet keys with the family anchored on a chosen key.");
        placementCommand.Add(maxSizeOption);
        placementCommand.Add(maxPrimeOption);
        placementCommand.Add(maxLcmOption);
        placementCommand.Add(placementLcmOption);
        placementCommand.Add(placementAtOption);
        placementCommand.Add(ktetOption);
        placementCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var lcms = parse.GetValue(placementLcmOption) ?? Array.Empty<int>();
            var at = parse.GetValue(placementAtOption);
            var k = parse.GetValue(ktetOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (k < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }
            if (lcms.Length == 0) { AnsiConsole.MarkupLine("[red]--lcm must contain at least one value.[/]"); return 1; }
            var badLcms = lcms.Where(v => v < 1).ToList();
            if (badLcms.Count > 0) { AnsiConsole.MarkupLine($"[red]--lcm values must be ≥ 1; got {string.Join(", ", badLcms)}.[/]"); return 1; }
            if (at < 0 || at >= k) { AnsiConsole.MarkupLine($"[red]--at value {at} is outside [[0, {k - 1}]].[/]"); return 1; }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);

            var rows = new List<(LcmFamily family, Placement placement)>(lcms.Length);
            var missing = new List<int>();
            foreach (var lcm in lcms)
            {
                var family = families.FirstOrDefault(f => f.Lcm == lcm);
                if (family.Fractions is null || family.Fractions.Count == 0)
                {
                    missing.Add(lcm);
                    continue;
                }
                rows.Add((family, Placements.Compute(family, at, k)));
            }

            if (missing.Count > 0)
            {
                AnsiConsole.MarkupLine($"[red]No LCM family exists at L={string.Join(", ", missing)} under --max-size {maxSize} / --max-prime {maxPrime} / --max-lcm {maxLcm}.[/]");
                return 1;
            }

            PlacementTableRenderer.Render(rows, k);
            return 0;
        });

        var lcmSweepOption = new Option<int>("--lcm-sweep")
        {
            Description = "LCM of family A — swept across all k placements (at = 0..ktet-1).",
            Required = true,
        };
        var lcmRefOption = new Option<int>("--lcm-ref")
        {
            Description = "LCM of family B — held at @0 as the reference for overlap.",
            Required = true,
        };

        var familyOverlapCommand = new Command(
            "family-overlap",
            "Sweep all k placements of family A and report the key intersection with family B held at @0.");
        familyOverlapCommand.Add(maxSizeOption);
        familyOverlapCommand.Add(maxPrimeOption);
        familyOverlapCommand.Add(maxLcmOption);
        familyOverlapCommand.Add(lcmSweepOption);
        familyOverlapCommand.Add(lcmRefOption);
        familyOverlapCommand.Add(ktetOption);
        familyOverlapCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var lcmSweep = parse.GetValue(lcmSweepOption);
            var lcmRef = parse.GetValue(lcmRefOption);
            var k = parse.GetValue(ktetOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (k < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }
            if (lcmSweep < 1) { AnsiConsole.MarkupLine("[red]--lcm-sweep must be ≥ 1.[/]"); return 1; }
            if (lcmRef < 1) { AnsiConsole.MarkupLine("[red]--lcm-ref must be ≥ 1.[/]"); return 1; }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var familyA = families.FirstOrDefault(f => f.Lcm == lcmSweep);
            if (familyA.Fractions is null || familyA.Fractions.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No LCM family exists at L={lcmSweep} under --max-size {maxSize} / --max-prime {maxPrime} / --max-lcm {maxLcm}.[/]");
                return 1;
            }
            var familyB = families.FirstOrDefault(f => f.Lcm == lcmRef);
            if (familyB.Fractions is null || familyB.Fractions.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No LCM family exists at L={lcmRef} under --max-size {maxSize} / --max-prime {maxPrime} / --max-lcm {maxLcm}.[/]");
                return 1;
            }

            var (bKeysAtZero, rows) = Placements.OverlapSweep(familyA, familyB, k);
            FamilyOverlapTableRenderer.Render(lcmSweep, lcmRef, k, bKeysAtZero, rows);
            return 0;
        });

        var keySupersetsKeysOption = new Option<int[]>("--keys")
        {
            Description = "Target key indices on a --ktet keyboard, space-separated. Any integer is accepted and octave-normalized into [0, ktet-1] (e.g. 14 → 2, -1 → ktet-1). Duplicates are folded.",
            Required = true,
            AllowMultipleArgumentsPerToken = true,
        };

        var keySupersetsCommand = new Command(
            "key-supersets",
            "Enumerate every (lcm, at) placement whose k-tet keys are a superset of the given --keys; ranked by smallest extra-keys count.");
        keySupersetsCommand.Add(maxSizeOption);
        keySupersetsCommand.Add(maxPrimeOption);
        keySupersetsCommand.Add(maxLcmOption);
        keySupersetsCommand.Add(keySupersetsKeysOption);
        keySupersetsCommand.Add(ktetOption);
        keySupersetsCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var keys = parse.GetValue(keySupersetsKeysOption) ?? Array.Empty<int>();
            var k = parse.GetValue(ktetOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (k < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }
            if (keys.Length == 0) { AnsiConsole.MarkupLine("[red]--keys must contain at least one value.[/]"); return 1; }

            // Octave-normalize keys into [0, k) so callers can pass any integer (e.g. 14 → 2, -1 → k-1).
            var dedupKeys = keys.Select(key => ((key % k) + k) % k).Distinct().OrderBy(x => x).ToList();
            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var rows = Placements.FindSupersets(dedupKeys, families, k);
            KeySupersetsTableRenderer.Render(dedupKeys, k, rows);
            return 0;
        });

        var superpositionsKeysOption = new Option<int[]>("--keys")
        {
            Description = "Target key indices on a --ktet keyboard, space-separated. Any integer is accepted and octave-normalized into [0, ktet-1] (e.g. 14 → 2, -1 → ktet-1). Duplicates are folded.",
            Required = true,
            AllowMultipleArgumentsPerToken = true,
        };
        var superpositionsMinBlockLcmOption = new Option<int>("--min-block-lcm")
        {
            Description = "Smallest LCM allowed for a building-block family (default 2, excluding the unison). Raise it to compose the target from larger patterns only, e.g. --min-block-lcm 8 to use the 8/9/10/12 families.",
            DefaultValueFactory = _ => 2,
        };
        var superpositionsMaxBlockLcmOption = new Option<int?>("--max-block-lcm")
        {
            Description = "Largest LCM allowed for a building-block family (defaults to --max-lcm). Lower it to compose the target from smaller patterns only, e.g. --max-block-lcm 12.",
            DefaultValueFactory = _ => null,
        };
        var superpositionsMaxResultsOption = new Option<int>("--max-results")
        {
            Description = "Cap on the number of decompositions listed.",
            DefaultValueFactory = _ => 200,
        };
        var superpositionsAnyReferenceOption = new Option<bool>("--any-reference")
        {
            Description = "Allow pieces to sit at different reference keys (anchors). Off by default, which keeps only decompositions whose pieces share one reference key.",
        };

        var superpositionsCommand = new Command(
            "superpositions",
            "Enumerate minimal/irredundant ways to cover --keys as a union (superposition) of LCM-family placements. Building blocks may extend past the target; each reported decomposition lists its extra keys. Every piece uniquely covers at least one target key.");
        superpositionsCommand.Add(maxSizeOption);
        superpositionsCommand.Add(maxPrimeOption);
        superpositionsCommand.Add(maxLcmOption);
        superpositionsCommand.Add(superpositionsKeysOption);
        superpositionsCommand.Add(superpositionsMinBlockLcmOption);
        superpositionsCommand.Add(superpositionsMaxBlockLcmOption);
        superpositionsCommand.Add(superpositionsMaxResultsOption);
        superpositionsCommand.Add(superpositionsAnyReferenceOption);
        superpositionsCommand.Add(ktetOption);
        superpositionsCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var keys = parse.GetValue(superpositionsKeysOption) ?? Array.Empty<int>();
            var minBlockLcm = parse.GetValue(superpositionsMinBlockLcmOption);
            var maxBlockLcm = parse.GetValue(superpositionsMaxBlockLcmOption) ?? maxLcm;
            var maxResults = parse.GetValue(superpositionsMaxResultsOption);
            var anyReference = parse.GetValue(superpositionsAnyReferenceOption);
            var k = parse.GetValue(ktetOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (k < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }
            if (keys.Length == 0) { AnsiConsole.MarkupLine("[red]--keys must contain at least one value.[/]"); return 1; }
            if (minBlockLcm < 2) { AnsiConsole.MarkupLine("[red]--min-block-lcm must be ≥ 2.[/]"); return 1; }
            if (maxBlockLcm < minBlockLcm) { AnsiConsole.MarkupLine("[red]--max-block-lcm must be ≥ --min-block-lcm.[/]"); return 1; }
            if (maxResults < 1) { AnsiConsole.MarkupLine("[red]--max-results must be ≥ 1.[/]"); return 1; }

            // Octave-normalize keys into [0, k) so callers can pass any integer (e.g. 14 → 2, -1 → k-1).
            var dedupKeys = keys.Select(key => ((key % k) + k) % k).Distinct().OrderBy(x => x).ToList();
            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var (rows, truncated) = Superpositions.Enumerate(dedupKeys, families, k, minBlockLcm, maxBlockLcm, maxResults, uniqueReference: !anyReference);
            SuperpositionsTableRenderer.Render(dedupKeys, k, minBlockLcm, maxBlockLcm, rows, truncated, uniqueReference: !anyReference);
            return 0;
        });

        var chordMelodyChordKeysOption = new Option<int[]>("--chord-keys")
        {
            Description = "Chord key indices on a --ktet keyboard (each in [0, ktet-1]), space-separated. Duplicates are folded.",
            Required = true,
            AllowMultipleArgumentsPerToken = true,
        };

        var chordMelodyCommand = new Command(
            "chord-melody",
            "Matrix view: rows are maximal-LCM placements containing the given chord; columns are the k-tet keys; cells mark whether each key is in the placement. Reading column K shows which placements survive playing key K as melody.");
        chordMelodyCommand.Add(maxSizeOption);
        chordMelodyCommand.Add(maxPrimeOption);
        chordMelodyCommand.Add(maxLcmOption);
        chordMelodyCommand.Add(chordMelodyChordKeysOption);
        chordMelodyCommand.Add(ktetOption);
        chordMelodyCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var chordKeys = parse.GetValue(chordMelodyChordKeysOption) ?? Array.Empty<int>();
            var k = parse.GetValue(ktetOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (k < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }
            if (chordKeys.Length == 0) { AnsiConsole.MarkupLine("[red]--chord-keys must contain at least one value.[/]"); return 1; }
            foreach (var key in chordKeys)
            {
                if (key < 0 || key >= k)
                {
                    AnsiConsole.MarkupLine($"[red]--chord-keys value {key} is outside [[0, {k - 1}]].[/]");
                    return 1;
                }
            }

            var dedupChord = chordKeys.Distinct().OrderBy(x => x).ToList();
            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var relations = FamilyRelations.Compute(families);
            var maximalLcms = Placements.MaximalLcms(families, relations);
            var placements = Placements.FindMaximalContaining(dedupChord, families, relations, k);
            ChordMelodyTableRenderer.Render(dedupChord, maximalLcms, k, placements);
            return 0;
        });

        var voicingsLcmOption = new Option<int?>("--lcm")
        {
            Description = "LCM (wave pattern length) of the family whose @0 placement we enumerate voicings of. Mutually exclusive with --keys.",
            DefaultValueFactory = _ => null,
        };

        var voicingsKeysOption = new Option<int[]>("--keys")
        {
            Description = "Key indices on a --ktet keyboard (each in [0, ktet-1]), space-separated. Duplicates are folded. Mutually exclusive with --lcm.",
            AllowMultipleArgumentsPerToken = true,
        };

        var voicingsCommand = new Command(
            "voicings",
            "Enumerate ascending voicings of an L@0 placement (via --lcm) or a direct key set (via --keys). Voicings avoid the semitone (interval 1) and visit each key exactly once. Per root, only the lowest-penalty voicings are emitted; penalty is 0 for triadic intervals {3,4}, 1 for {2,5}, and (i-4) for i≥6 wides.");
        voicingsCommand.Add(maxSizeOption);
        voicingsCommand.Add(maxPrimeOption);
        voicingsCommand.Add(maxLcmOption);
        voicingsCommand.Add(voicingsLcmOption);
        voicingsCommand.Add(voicingsKeysOption);
        voicingsCommand.Add(ktetOption);
        voicingsCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var lcm = parse.GetValue(voicingsLcmOption);
            var keys = parse.GetValue(voicingsKeysOption) ?? Array.Empty<int>();
            var k = parse.GetValue(ktetOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (k < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }

            var hasLcm = lcm.HasValue;
            var hasKeys = keys.Length > 0;
            if (hasLcm == hasKeys)
            {
                AnsiConsole.MarkupLine("[red]must provide exactly one of --lcm or --keys.[/]");
                return 1;
            }

            if (hasKeys)
            {
                foreach (var key in keys)
                {
                    if (key < 0 || key >= k)
                    {
                        AnsiConsole.MarkupLine($"[red]--keys value {key} is outside [[0, {k - 1}]].[/]");
                        return 1;
                    }
                }

                var dedupKeys = keys.Distinct().OrderBy(x => x).ToList();
                var voicings = Voicings.EnumerateBestPerRoot(dedupKeys, k);
                VoicingsTableRenderer.Render(lcm: null, at: null, k, dedupKeys, voicings);
                return 0;
            }

            if (lcm < 1) { AnsiConsole.MarkupLine("[red]--lcm must be ≥ 1.[/]"); return 1; }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var family = families.FirstOrDefault(f => f.Lcm == lcm);
            if (family.Fractions is null || family.Fractions.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No LCM family exists at L={lcm} under --max-size {maxSize} / --max-prime {maxPrime} / --max-lcm {maxLcm}.[/]");
                return 1;
            }

            var placement = Placements.Compute(family, at: 0, k);
            var familyVoicings = Voicings.EnumerateBestPerRoot(placement.Keys, k);
            VoicingsTableRenderer.Render(lcm, at: 0, k, placement.Keys, familyVoicings);
            return 0;
        });

        var subsetsLcmOption = new Option<int?>("--lcm")
        {
            Description = "LCM (wave pattern length) of the family whose placement's keys form the base set. Combined with --at. Mutually exclusive with --keys.",
            DefaultValueFactory = _ => null,
        };
        var subsetsAtOption = new Option<int>("--at")
        {
            Description = "Anchor key for the --lcm placement (the family's reference maps here). Default 0.",
            DefaultValueFactory = _ => 0,
        };
        var subsetsKeysOption = new Option<int[]>("--keys")
        {
            Description = "Base key indices on a --ktet keyboard, space-separated. Any integer is accepted and octave-normalized into [0, ktet-1]. Duplicates are folded. Mutually exclusive with --lcm.",
            AllowMultipleArgumentsPerToken = true,
        };
        var subsetsOnlyFullMatchesOption = new Option<bool>("--only-full-matches")
        {
            Description = "Restrict to strict full matches (every subset key bins uniquely). By default ambiguous full matches are included too.",
            DefaultValueFactory = _ => false,
        };
        var subsetsMaxResultsOption = new Option<int>("--max-results")
        {
            Description = "Maximum number of subset rows to display. Default 200.",
            DefaultValueFactory = _ => 200,
        };
        var subsetsAmbiguousMatchesOption = new Option<int>("--ambiguous-matches")
        {
            Description = "Maximum number of candidate LCMs to list per ambiguous cell, ascending best-fit first (e.g. \"8? 9?\"). Default 1.",
            DefaultValueFactory = _ => 1,
        };

        var subsetsCommand = new Command(
            "subsets",
            "Enumerate every size-≥2 subset of a key set (from --lcm@--at or --keys) and key-sweep each one, reporting the LCM families those subsets full-match and the reference keys where they do. Surfaces the renormalized-subset relations of the lcm-families graph at the keyboard level.");
        subsetsCommand.Add(maxSizeOption);
        subsetsCommand.Add(maxPrimeOption);
        subsetsCommand.Add(maxLcmOption);
        subsetsCommand.Add(subsetsLcmOption);
        subsetsCommand.Add(subsetsAtOption);
        subsetsCommand.Add(subsetsKeysOption);
        subsetsCommand.Add(ktetOption);
        subsetsCommand.Add(keySweepBinRadiusOption);
        subsetsCommand.Add(subsetsOnlyFullMatchesOption);
        subsetsCommand.Add(subsetsMaxResultsOption);
        subsetsCommand.Add(subsetsAmbiguousMatchesOption);
        subsetsCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var lcm = parse.GetValue(subsetsLcmOption);
            var at = parse.GetValue(subsetsAtOption);
            var keys = parse.GetValue(subsetsKeysOption) ?? Array.Empty<int>();
            var k = parse.GetValue(ktetOption);
            var binRadiusOverride = parse.GetValue(keySweepBinRadiusOption);
            var strictOnly = parse.GetValue(subsetsOnlyFullMatchesOption);
            var maxResults = parse.GetValue(subsetsMaxResultsOption);
            var ambiguousMatches = parse.GetValue(subsetsAmbiguousMatchesOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (k < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }
            if (maxResults < 1) { AnsiConsole.MarkupLine("[red]--max-results must be ≥ 1.[/]"); return 1; }
            if (ambiguousMatches < 1) { AnsiConsole.MarkupLine("[red]--ambiguous-matches must be ≥ 1.[/]"); return 1; }
            if (binRadiusOverride is double overrideValue && !(overrideValue > 0.0 && overrideValue < 1.0))
            {
                AnsiConsole.MarkupLine("[red]--bin-radius must be in (0, 1).[/]");
                return 1;
            }

            var hasLcm = lcm.HasValue;
            var hasKeys = keys.Length > 0;
            if (hasLcm == hasKeys)
            {
                AnsiConsole.MarkupLine("[red]must provide exactly one of --lcm or --keys.[/]");
                return 1;
            }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            if (fractions.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No good fractions under --max-size {maxSize} --max-prime {maxPrime}.[/]");
                return 1;
            }

            IReadOnlyList<int> baseKeys;
            string inputLabel;
            if (hasKeys)
            {
                baseKeys = keys.Select(key => ((key % k) + k) % k).Distinct().OrderBy(x => x).ToList();
                inputLabel = $"{{{string.Join(" ", baseKeys)}}}";
            }
            else
            {
                if (lcm < 1) { AnsiConsole.MarkupLine("[red]--lcm must be ≥ 1.[/]"); return 1; }

                var families = LcmFamilies.Compute(fractions, maxLcm);
                var family = families.FirstOrDefault(f => f.Lcm == lcm);
                if (family.Fractions is null || family.Fractions.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[red]No LCM family exists at L={lcm} under --max-size {maxSize} / --max-prime {maxPrime} / --max-lcm {maxLcm}.[/]");
                    return 1;
                }

                var normalizedAt = ((at % k) + k) % k;
                var placement = Placements.Compute(family, normalizedAt, k);
                baseKeys = placement.Keys.Distinct().OrderBy(x => x).ToList();
                inputLabel = $"{lcm}@{normalizedAt}";
            }

            if (baseKeys.Count < 2)
            {
                AnsiConsole.MarkupLine("[red]need at least 2 distinct base keys to form subsets.[/]");
                return 1;
            }

            var effectiveRadius = binRadiusOverride ?? KeysNeeded.WorstCaseForK(fractions, k).Radius;
            var (matches, truncated) = Subsets.Enumerate(baseKeys, fractions, k, effectiveRadius, strictOnly, maxResults);
            SubsetsTableRenderer.Render(inputLabel, baseKeys, matches, k, effectiveRadius, strictOnly, truncated, ambiguousMatches);
            return 0;
        });

        var tableCommand = new Command("table", "Console-table output commands.");
        tableCommand.Add(goodFractionsCommand);
        tableCommand.Add(lcmFamiliesCommand);
        tableCommand.Add(binOverlapsCommand);
        tableCommand.Add(octaveSweepCommand);
        tableCommand.Add(ktetCutoffsCommand);
        tableCommand.Add(keySweepCommand);
        tableCommand.Add(placementCommand);
        tableCommand.Add(familyOverlapCommand);
        tableCommand.Add(keySupersetsCommand);
        tableCommand.Add(superpositionsCommand);
        tableCommand.Add(chordMelodyCommand);
        tableCommand.Add(voicingsCommand);
        tableCommand.Add(subsetsCommand);

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
        var differenceOnlyOption = new Option<bool>("--difference-only")
        {
            Description = "With --mode difference, plot only the residual (no parent / subset reference sums).",
            DefaultValueFactory = _ => false,
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
        plotLcmFamiliesCommand.Add(differenceOnlyOption);
        plotLcmFamiliesCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var lcm = parse.GetValue(lcmOption);
            var samplesPerPeriod = parse.GetValue(samplesPerPeriodOption);
            var modeString = parse.GetValue(plotModeOption) ?? "all";
            var subsetLcm = parse.GetValue(subsetLcmOption);
            var differenceOnly = parse.GetValue(differenceOnlyOption);

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
            if (differenceOnly && mode != PlotMode.Difference)
            {
                AnsiConsole.MarkupLine("[red]--difference-only is only valid with --mode difference.[/]");
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
            var modeSlug = mode == PlotMode.Difference && differenceOnly ? "difference-only" : modeString;
            var baseName = mode == PlotMode.All
                ? $"lcm-family-{lcm}"
                : $"lcm-family-{lcm}-{modeSlug}";
            var fileName = subFamily is null
                ? $"{baseName}.png"
                : $"{baseName}-sub{subFamily.Value.Lcm}.png";
            var outputPath = Path.Combine(outputDir, fileName);
            LcmFamilyWaveformRenderer.Render(family, outputPath, samplesPerPeriod, mode, subFamily, differenceOnly);

            AnsiConsole.WriteLine(Path.GetFullPath(outputPath));
            return 0;
        });

        var plotCommand = new Command("plot", "Plot-output commands (PNG).");
        plotCommand.Add(plotLcmFamiliesCommand);

        var root = new RootCommand("Melodroid — music research from first principles.");
        root.Add(tableCommand);
        root.Add(graphCommand);
        root.Add(plotCommand);

        return root.Parse(args).Invoke();
    }
}
