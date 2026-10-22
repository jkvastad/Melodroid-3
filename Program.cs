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

        var renormLcmOption = new Option<int>("--lcm")
        {
            Description = "LCM (wave pattern length) of the family to renormalize onto each of its members.",
            Required = true,
        };

        var renormApproximateOption = new Option<bool>("--approximate")
        {
            Description = "Snap non-good renormalized fractions to the nearest good fraction and report the exact bin radius (and %) each renormalization needs to become all-good.",
        };

        var renormPerSnapCOption = new Option<bool>("--per-snap-c")
        {
            Description = "With --approximate, annotate each snapped good-column value with the exact bin radius (and %) that snap required.",
        };

        var renormLcmTradeoffOption = new Option<bool>("--lcm-tradeoff")
        {
            Description = "With --approximate, add a per-row LCM↔radius frontier: for each achievable wave-pattern length ≤ --max-lcm, the smallest bin radius (within --max-c) needed to snap the renormalization down to it. Accepting a worse per-fraction snap can hold the overall LCM lower.",
        };

        var renormMaxCOption = new Option<double>("--max-c")
        {
            Description = "With --lcm-tradeoff, the maximum bin radius c (0..1) a candidate snap may cost; caps no assignment can satisfy within it are omitted. Default 0.05 (5 %), intentionally above the ~0.5–1 % JND to leave the frontier room.",
            DefaultValueFactory = _ => 0.05,
        };

        var renormPlacementsOption = new Option<bool>("--placements")
        {
            Description = "With --approximate, add a Placement column expressing each rotation as a k-tet placement: '= L'@k' (exact isomorphism) or '⊆ L'@k' (renormalized subset) for rotations that reach a good LCM (≤ --max-lcm) within the syntonic comma (c ≤ 1/161); other rows show —.",
        };

        var renormKtetOption = new Option<int>("--ktet")
        {
            Description = "With --placements, the equal-tempered keyboard size used to place each rotation's base fraction. Default 12.",
            DefaultValueFactory = _ => 12,
        };

        var renormalizationsCommand = new Command(
            "renormalizations",
            "Renormalize one LCM family onto each of its member fractions, showing the resulting (isomorphic) fraction sets.");
        renormalizationsCommand.Add(maxSizeOption);
        renormalizationsCommand.Add(maxPrimeOption);
        renormalizationsCommand.Add(maxLcmOption);
        renormalizationsCommand.Add(renormLcmOption);
        renormalizationsCommand.Add(renormApproximateOption);
        renormalizationsCommand.Add(renormPerSnapCOption);
        renormalizationsCommand.Add(renormLcmTradeoffOption);
        renormalizationsCommand.Add(renormMaxCOption);
        renormalizationsCommand.Add(renormPlacementsOption);
        renormalizationsCommand.Add(renormKtetOption);
        renormalizationsCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var lcm = parse.GetValue(renormLcmOption);
            var approximate = parse.GetValue(renormApproximateOption);
            var perSnapC = parse.GetValue(renormPerSnapCOption);
            var lcmTradeoff = parse.GetValue(renormLcmTradeoffOption);
            var maxC = parse.GetValue(renormMaxCOption);
            var placementsFlag = parse.GetValue(renormPlacementsOption);
            var ktet = parse.GetValue(renormKtetOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (lcm < 1) { AnsiConsole.MarkupLine("[red]--lcm must be ≥ 1.[/]"); return 1; }
            if (perSnapC && !approximate) { AnsiConsole.MarkupLine("[red]--per-snap-c requires --approximate.[/]"); return 1; }
            if (lcmTradeoff && !approximate) { AnsiConsole.MarkupLine("[red]--lcm-tradeoff requires --approximate.[/]"); return 1; }
            if (lcmTradeoff && maxC <= 0) { AnsiConsole.MarkupLine("[red]--max-c must be > 0.[/]"); return 1; }
            if (placementsFlag && !approximate) { AnsiConsole.MarkupLine("[red]--placements requires --approximate.[/]"); return 1; }
            if (placementsFlag && ktet < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var family = families.FirstOrDefault(f => f.Lcm == lcm);
            if (family.Fractions is null || family.Fractions.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No LCM family exists at L={lcm} under --max-size {maxSize} / --max-prime {maxPrime} / --max-lcm {maxLcm}.[/]");
                return 1;
            }

            if (approximate)
            {
                var approxRows = family.Fractions
                    .Select(b => (b, RenormalizationApproximation.Snap(
                        Renormalization.Renormalize(family.Fractions, b), fractions)))
                    .ToList();
                IReadOnlyList<IReadOnlyList<SnapFrontierPoint>>? frontiers = lcmTradeoff
                    ? family.Fractions
                        .Select(b => RenormalizationApproximation.SnapFrontier(
                            Renormalization.Renormalize(family.Fractions, b), fractions, maxLcm, maxC))
                        .ToList()
                    : null;
                IReadOnlyList<PlacementImage>? placementImages = placementsFlag
                    ? approxRows
                        .Select(r => RenormalizationApproximation.AsPlacement(r.Item2, r.b, ktet, maxLcm, families))
                        .ToList()
                    : null;
                RenormalizationTableRenderer.RenderApproximate(family, approxRows, perSnapC: perSnapC, frontiers: frontiers, placements: placementImages);
                return 0;
            }

            var rows = family.Fractions
                .Select(b => (b, Renormalization.Renormalize(family.Fractions, b)))
                .ToList();
            RenormalizationTableRenderer.Render(family, rows);
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

        var keySupersetsCompareOption = new Option<int[]>("--compare")
        {
            Description = "Optional second key set to compare against --keys. When given, supersets are split into three tables: common to both, unique to --keys, and unique to --compare. Octave-normalized and folded like --keys.",
            AllowMultipleArgumentsPerToken = true,
        };

        var keySupersetsCollapseOption = new Option<bool>("--collapse")
        {
            Description = "Collapse placements that cover the identical key set (isomorphic families sweep out the same keys) down to the lowest-LCM representative.",
        };

        var keySupersetsCommand = new Command(
            "key-supersets",
            "Enumerate every (lcm, at) placement whose k-tet keys are a superset of the given --keys; ranked by smallest extra-keys count. Pass --compare to split supersets against a second key set.");
        keySupersetsCommand.Add(maxSizeOption);
        keySupersetsCommand.Add(maxPrimeOption);
        keySupersetsCommand.Add(maxLcmOption);
        keySupersetsCommand.Add(keySupersetsKeysOption);
        keySupersetsCommand.Add(keySupersetsCompareOption);
        keySupersetsCommand.Add(keySupersetsCollapseOption);
        keySupersetsCommand.Add(ktetOption);
        keySupersetsCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var keys = parse.GetValue(keySupersetsKeysOption) ?? Array.Empty<int>();
            var compare = parse.GetValue(keySupersetsCompareOption) ?? Array.Empty<int>();
            var collapse = parse.GetValue(keySupersetsCollapseOption);
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
            if (collapse) rows = Placements.CollapseIsomorphic(rows);

            if (compare.Length == 0)
            {
                KeySupersetsTableRenderer.Render(dedupKeys, k, rows);
                return 0;
            }

            var compareKeys = compare.Select(key => ((key % k) + k) % k).Distinct().OrderBy(x => x).ToList();
            var compareRows = Placements.FindSupersets(compareKeys, families, k);
            if (collapse) compareRows = Placements.CollapseIsomorphic(compareRows);
            var (common, onlyA, onlyB) = Placements.CompareSupersets(rows, compareRows);
            KeySupersetsTableRenderer.RenderComparison(dedupKeys, compareKeys, k, common, onlyA, onlyB);
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
        var superpositionsCollapseAliasesOption = new Option<bool>("--collapse-aliases")
        {
            Description = "Collapse decompositions that yield the identical union at the same reference to a single lowest-LCM representative, folding away isomorphic/renormalisation aliases (e.g. 5@0+8@0 = 8@0+10@0 = 8@0+20@0). Off by default.",
        };
        var superpositionsReferenceOption = new Option<int[]>("--reference")
        {
            Description = "Restrict to unique-reference decompositions anchored on the given key(s), space-separated. Any integer is accepted and octave-normalized into [0, ktet-1]. Incompatible with --any-reference. Empty (default) keeps all references.",
            AllowMultipleArgumentsPerToken = true,
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
        superpositionsCommand.Add(superpositionsCollapseAliasesOption);
        superpositionsCommand.Add(superpositionsReferenceOption);
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
            var collapseAliases = parse.GetValue(superpositionsCollapseAliasesOption);
            var referenceKeys = parse.GetValue(superpositionsReferenceOption) ?? Array.Empty<int>();
            var k = parse.GetValue(ktetOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (k < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }
            if (keys.Length == 0) { AnsiConsole.MarkupLine("[red]--keys must contain at least one value.[/]"); return 1; }
            if (minBlockLcm < 2) { AnsiConsole.MarkupLine("[red]--min-block-lcm must be ≥ 2.[/]"); return 1; }
            if (maxBlockLcm < minBlockLcm) { AnsiConsole.MarkupLine("[red]--max-block-lcm must be ≥ --min-block-lcm.[/]"); return 1; }
            if (maxResults < 1) { AnsiConsole.MarkupLine("[red]--max-results must be ≥ 1.[/]"); return 1; }
            if (anyReference && referenceKeys.Length > 0) { AnsiConsole.MarkupLine("[red]--reference cannot be combined with --any-reference (any-reference rows share no single anchor).[/]"); return 1; }

            // Octave-normalize keys into [0, k) so callers can pass any integer (e.g. 14 → 2, -1 → k-1).
            var dedupKeys = keys.Select(key => ((key % k) + k) % k).Distinct().OrderBy(x => x).ToList();
            var dedupReferences = referenceKeys.Select(key => ((key % k) + k) % k).Distinct().ToList();
            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var (rows, truncated) = Superpositions.Enumerate(dedupKeys, families, k, minBlockLcm, maxBlockLcm, maxResults, uniqueReference: !anyReference);
            rows = Superpositions.ApplyFilters(rows, dedupReferences, collapseAliases);
            SuperpositionsTableRenderer.Render(dedupKeys, k, minBlockLcm, maxBlockLcm, rows, truncated, uniqueReference: !anyReference);
            return 0;
        });

        var chordMelodyChordKeysOption = new Option<int[]>("--chord-keys")
        {
            Description = "Chord key indices on a --ktet keyboard (each in [0, ktet-1]), space-separated. Duplicates are folded.",
            Required = true,
            AllowMultipleArgumentsPerToken = true,
        };

        var chordMelodyDropRenormalizedOption = new Option<bool>("--drop-renormalized-subsets")
        {
            Description = "Also treat a family that is a renormalized subset of a larger allowed family as dominated (filtered out), not just literal subsets. Off by default, which keeps renormalized subsets as their own rows.",
            DefaultValueFactory = _ => false,
        };

        var chordMelodyDropCollapsedOption = new Option<bool>("--drop-collapsed")
        {
            Description = "Drop unstable placements whose collapsing key is one of --chord-keys — they have collapsed onto a smaller pattern under preference: lcm-15 (key (At+8) mod 12, collapses to lcm-24) and lcm-20 (key (At+3) mod 12, collapses to 15@(At+7)). 12-tet only; no effect on other families or ktets.",
            DefaultValueFactory = _ => false,
        };

        var chordMelodyStable15Option = new Option<bool>("--stable-15")
        {
            Description = "Show each lcm-15 placement in its stable form 15s@At: drop the collapsing key (At+8) mod 12 and label the row 15s. 12-tet only; no effect on other families or ktets. Combine with --drop-collapsed to first remove rows that collapsed onto a chord key.",
            DefaultValueFactory = _ => false,
        };

        var chordMelodyCommand = new Command(
            "chord-melody",
            "Matrix view: rows are maximal-LCM placements containing the given chord; columns are the k-tet keys; cells mark whether each key is in the placement. Reading column K shows which placements survive playing key K as melody.");
        chordMelodyCommand.Add(maxSizeOption);
        chordMelodyCommand.Add(maxPrimeOption);
        chordMelodyCommand.Add(maxLcmOption);
        chordMelodyCommand.Add(chordMelodyChordKeysOption);
        chordMelodyCommand.Add(chordMelodyDropRenormalizedOption);
        chordMelodyCommand.Add(chordMelodyDropCollapsedOption);
        chordMelodyCommand.Add(chordMelodyStable15Option);
        chordMelodyCommand.Add(ktetOption);
        chordMelodyCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var chordKeys = parse.GetValue(chordMelodyChordKeysOption) ?? Array.Empty<int>();
            var dropRenormalizedSubsets = parse.GetValue(chordMelodyDropRenormalizedOption);
            var dropCollapsed = parse.GetValue(chordMelodyDropCollapsedOption);
            var stable15 = parse.GetValue(chordMelodyStable15Option);
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
            var maximalLcms = Placements.MaximalLcms(families, relations, dropRenormalizedSubsets);
            var placements = Placements.FindMaximalContaining(dedupChord, families, relations, k, dropRenormalizedSubsets);
            if (dropCollapsed) placements = Placements.DropCollapsed(placements, dedupChord, k);
            if (stable15) placements = Placements.StableFifteen(placements, k);
            ChordMelodyTableRenderer.Render(dedupChord, maximalLcms, k, placements);
            return 0;
        });

        var progressionOriginKeysOption = new Option<int[]>("--origin-keys")
        {
            Description = "Origin-chord key indices on a 12-tet keyboard (each in [0, 11]), space-separated. Any set of keys. Duplicates are folded.",
            Required = true,
            AllowMultipleArgumentsPerToken = true,
        };

        var progressionTargetKeysOption = new Option<int[]>("--target-keys")
        {
            Description = "Target-chord key indices (each in [0, 11]), space-separated. When given, print every origin→target progression as explicit origin/bridge/target triplet rows. When omitted, enumerate all major/minor (+ --include-dim) triads as candidate targets and print the per-target table.",
            AllowMultipleArgumentsPerToken = true,
        };

        var progressionMinSubsetNotesOption = new Option<int>("--min-subset-notes")
        {
            Description = "Minimum number of keys a good-LCM family placement must have to serve as a shared subset S in the subset principle. Default 2 (dyads allowed); raise to restrict to larger bridging families.",
            DefaultValueFactory = _ => 2,
        };

        var progressionIncludeDimOption = new Option<bool>("--include-dim")
        {
            Description = "Convenience form only (no --target-keys): also include the 12 diminished triads as candidate targets (36 rows instead of 24). Default false (major/minor only).",
            DefaultValueFactory = _ => false,
        };

        var progressionCollapseIsomorphicOption = new Option<bool>("--collapse-isomorphic")
        {
            Description = "Declutter: relabel each placement to the representative of its key set, preferring a power-of-2 LCM (4 over 3, 8 over 9/10/12), then lowest LCM, then anchor, and dedupe. Categories are unaffected. Default off (raw labels).",
            DefaultValueFactory = _ => false,
        };

        var progressionPoolMaximalOption = new Option<bool>("--pool-maximal")
        {
            Description = "Declutter: keep only the maximal containing placements P_A/P_B (by key-set inclusion) and the maximal shared subsets S. Lossless for existence, so categories are unaffected. Default off (raw).",
            DefaultValueFactory = _ => false,
        };

        var progressionAllBridgesOption = new Option<bool>("--all-bridges")
        {
            Description = "Convenience form only: show every bridge in each Via cell instead of capping at 6 and summarising the rest as …(+M). Cells grow taller (wrapped across lines) as needed.",
            DefaultValueFactory = _ => false,
        };

        var progressionDropCollapsedOption = new Option<string>("--drop-collapsed")
        {
            Description = "Disqualify unstable placements collapsed by their respective chord (collapsing key is a chord key) — lcm-15 (key (At+8) mod 12) and lcm-20 (key (At+3) mod 12) — so they no longer count toward categories or appear as bridges: 'none' (default), 'origin' (origin-chord side), 'target' (target side), or 'both'. 12-tet only.",
            DefaultValueFactory = _ => "none",
        };

        var progressionCommand = new Command(
            "progression",
            "Formalise the two progression principles for 12-tet chords. With --target-keys: print every origin→target progression as explicit origin/bridge/target placement triplets (superset = one good-LCM placement holds both chords; subset = P_A·S·P_B via a shared subset family S of ≥ --min-subset-notes keys). Without --target-keys: enumerate every major/minor (+ --include-dim) triad as a candidate target and categorise each as superset / subset / both / neither. Bridge lists are raw by default; --collapse-isomorphic and --pool-maximal declutter them without changing categories. 12-tet only.");
        progressionCommand.Add(maxSizeOption);
        progressionCommand.Add(maxPrimeOption);
        progressionCommand.Add(maxLcmOption);
        progressionCommand.Add(progressionOriginKeysOption);
        progressionCommand.Add(progressionTargetKeysOption);
        progressionCommand.Add(progressionMinSubsetNotesOption);
        progressionCommand.Add(progressionIncludeDimOption);
        progressionCommand.Add(progressionCollapseIsomorphicOption);
        progressionCommand.Add(progressionPoolMaximalOption);
        progressionCommand.Add(progressionAllBridgesOption);
        progressionCommand.Add(progressionDropCollapsedOption);
        progressionCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var originKeys = parse.GetValue(progressionOriginKeysOption) ?? Array.Empty<int>();
            var targetKeys = parse.GetValue(progressionTargetKeysOption) ?? Array.Empty<int>();
            var minSubsetNotes = parse.GetValue(progressionMinSubsetNotesOption);
            var includeDim = parse.GetValue(progressionIncludeDimOption);
            var collapseIsomorphic = parse.GetValue(progressionCollapseIsomorphicOption);
            var poolMaximal = parse.GetValue(progressionPoolMaximalOption);
            var allBridges = parse.GetValue(progressionAllBridgesOption);
            var dropCollapsedString = parse.GetValue(progressionDropCollapsedOption) ?? "none";

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (minSubsetNotes < 1) { AnsiConsole.MarkupLine("[red]--min-subset-notes must be ≥ 1.[/]"); return 1; }
            if (originKeys.Length == 0) { AnsiConsole.MarkupLine("[red]--origin-keys must contain at least one value.[/]"); return 1; }
            foreach (var key in originKeys)
            {
                if (key < 0 || key >= 12)
                {
                    AnsiConsole.MarkupLine($"[red]--origin-keys value {key} is outside [[0, 11]].[/]");
                    return 1;
                }
            }
            foreach (var key in targetKeys)
            {
                if (key < 0 || key >= 12)
                {
                    AnsiConsole.MarkupLine($"[red]--target-keys value {key} is outside [[0, 11]].[/]");
                    return 1;
                }
            }

            var dropCollapsed = dropCollapsedString.ToLowerInvariant() switch
            {
                "none" => CollapseFilter.None,
                "origin" => CollapseFilter.Origin,
                "target" => CollapseFilter.Target,
                "both" => CollapseFilter.Both,
                _ => (CollapseFilter?)null,
            };
            if (dropCollapsed is null)
            {
                AnsiConsole.MarkupLine($"[red]--drop-collapsed value '{dropCollapsedString}' is not one of none|origin|target|both.[/]");
                return 1;
            }

            var dedupOrigin = originKeys.Distinct().OrderBy(x => x).ToList();
            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);

            if (targetKeys.Length > 0)
            {
                var dedupTarget = targetKeys.Distinct().OrderBy(x => x).ToList();
                var (category, triplets) = ChordProgressions.ComputePair(
                    dedupOrigin, dedupTarget, families, minSubsetNotes,
                    collapseIsomorphic, poolMaximal, dropCollapsed.Value);
                ProgressionTableRenderer.RenderPair(
                    dedupOrigin, dedupTarget, minSubsetNotes, collapseIsomorphic, poolMaximal,
                    dropCollapsed.Value, category, triplets);
                return 0;
            }

            var targets = ChordProgressions.Compute(
                dedupOrigin, families, minSubsetNotes, includeDim,
                collapseIsomorphic, poolMaximal, dropCollapsed.Value);
            ProgressionTableRenderer.Render(
                dedupOrigin, minSubsetNotes, collapseIsomorphic, poolMaximal,
                dropCollapsed.Value, targets, allBridges);
            return 0;
        });

        var junctionScalesMinSubsetNotesOption = new Option<int>("--min-subset-notes")
        {
            Description = "Minimum number of keys the shared subset S (a good-LCM family placement bridging the pair) must have. Default 4, so S is a substantial 18/8 parent (harm's S=18 has 6 keys); lower to admit smaller bridges and more scales.",
            DefaultValueFactory = _ => 4,
        };

        var junctionScalesStableOption = new Option<bool>("--stable")
        {
            Description = "Use the stable form of any collapsing (prime-5: lcm-15/20) constituent and drop its collapse key from the union. Default on; harm@9 = 15@11·18@2·24@0 requires it. --stable false emits the raw unions (harm will not appear).",
            DefaultValueFactory = _ => true,
        };

        var junctionScalesCollapseIsomorphicOption = new Option<bool>("--collapse-isomorphic")
        {
            Description = "Declutter: relabel each constituent placement to the representative of its key set, preferring a power-of-2 LCM (4 over 3, 8 over 9/10/12), then lowest LCM, then anchor, and dedupe. Default off (raw labels).",
            DefaultValueFactory = _ => false,
        };

        var junctionScalesPoolMaximalOption = new Option<bool>("--pool-maximal")
        {
            Description = "Declutter: keep only the maximal containing placements P_A/P_B (by key-set inclusion) and the maximal shared subsets S. Default off (raw).",
            DefaultValueFactory = _ => false,
        };

        var junctionScalesDedupeTranspositionOption = new Option<bool>("--dedupe-transposition")
        {
            Description = "Fold the 12 transpositions of each scale to one representative (lowest constituent LCMs/anchors). Default on; turn off to list every anchored instance.",
            DefaultValueFactory = _ => true,
        };

        var junctionScalesCommand = new Command(
            "junction-scales",
            "Catalog non-LCM \"junction\" scales: each is the key-union of a distinct placement pair (P_A, P_B) sharing an LCM-family subset S of ≥ --min-subset-notes keys (the subset principle read as a scale). Emits the concrete 12-tet key set to play plus the P_A·S·P_B constituents. --stable (default) uses collapsing placements' stable form and drops their collapse keys from the union (harm@9 = 15@11·18@2·24@0 requires it). --dedupe-transposition (default) keeps one anchor per necklace; --collapse-isomorphic / --pool-maximal declutter labels. 12-tet only.");
        junctionScalesCommand.Add(maxSizeOption);
        junctionScalesCommand.Add(maxPrimeOption);
        junctionScalesCommand.Add(maxLcmOption);
        junctionScalesCommand.Add(junctionScalesMinSubsetNotesOption);
        junctionScalesCommand.Add(junctionScalesStableOption);
        junctionScalesCommand.Add(junctionScalesCollapseIsomorphicOption);
        junctionScalesCommand.Add(junctionScalesPoolMaximalOption);
        junctionScalesCommand.Add(junctionScalesDedupeTranspositionOption);
        junctionScalesCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var minSubsetNotes = parse.GetValue(junctionScalesMinSubsetNotesOption);
            var stable = parse.GetValue(junctionScalesStableOption);
            var collapseIsomorphic = parse.GetValue(junctionScalesCollapseIsomorphicOption);
            var poolMaximal = parse.GetValue(junctionScalesPoolMaximalOption);
            var dedupeTransposition = parse.GetValue(junctionScalesDedupeTranspositionOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (minSubsetNotes < 1) { AnsiConsole.MarkupLine("[red]--min-subset-notes must be ≥ 1.[/]"); return 1; }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var scales = JunctionScales.Enumerate(
                families, minSubsetNotes, stable, collapseIsomorphic, poolMaximal, dedupeTransposition);
            JunctionScaleTableRenderer.Render(
                scales, minSubsetNotes, stable, collapseIsomorphic, poolMaximal, dedupeTransposition);
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

        var chordsMinNotesOption = new Option<int>("--min-notes")
        {
            Description = "Minimum number of notes per chord. Default 2.",
            DefaultValueFactory = _ => 2,
        };
        var chordsMaxNotesOption = new Option<int>("--max-notes")
        {
            Description = "Maximum number of notes per chord. Default 7.",
            DefaultValueFactory = _ => 7,
        };
        var chordsMaxResultsOption = new Option<int>("--max-results")
        {
            Description = "Maximum number of chord rows to display. Default 1000.",
            DefaultValueFactory = _ => 1000,
        };
        var chordsNoMinorSecondsOption = new Option<bool>("--no-minor-seconds")
        {
            Description = "Drop every chord containing a minor second (any two notes a semitone "
                + "apart), including the bare dyad and the whole major-seventh class. 12-tet only.",
        };
        var chordsAllowMaj7Option = new Option<bool>("--allow-maj7")
        {
            Description = "Keep chords with a single semitone, re-voiced as a major seventh (top note "
                + "a semitone below the octave, e.g. 0 4 7 11); still drops chords with two or more "
                + "semitones. Implies --no-minor-seconds. 12-tet only.",
        };
        var chordsNoTritonesOption = new Option<bool>("--no-tritones")
        {
            Description = "Drop every chord containing a tritone (any two notes six semitones apart), "
                + "including tritone-stacked chords like the diminished seventh 0 3 6 9. 12-tet only.",
        };
        var chordsAllowDimOption = new Option<bool>("--allow-dim")
        {
            Description = "Keep chords whose every tritone belongs to a diminished triad (p, p+3, p+6), "
                + "e.g. 0 3 6, 0 3 6 9, 0 3 6 8 (the dominant seventh); still drops chords with an "
                + "unjustified tritone. Implies --no-tritones. 12-tet only.",
        };
        var chordsLcmOnlyOption = new Option<bool>("--lcm-only")
        {
            Description = "Keep only chords that are a subset of ≥1 available LCM family — the LCM "
                + "approach; equivalent to hiding rows with an empty Placements column. Bounded by "
                + "--max-lcm.",
        };
        var chordsExplicitPlacementsOption = new Option<bool>("--explicit-placements")
        {
            Description = "Show every containing placement in the Placements column instead of "
                + "collapsing isomorphic duplicates (those covering the same keys) to the lowest LCM.",
        };

        var chordsCommand = new Command(
            "chords",
            "Enumerate the unique chords of a --ktet keyboard, deduplicated under transposition (each chord and all its transpositions count once, e.g. 0 4 7 = 0 3 8 = 0 5 9). Lists the canonical key set, its interval gaps around the octave, and its orbit size (number of distinct transpositions). Filtered by note count via --min-notes / --max-notes.");
        chordsCommand.Add(ktetOption);
        chordsCommand.Add(chordsMinNotesOption);
        chordsCommand.Add(chordsMaxNotesOption);
        chordsCommand.Add(chordsMaxResultsOption);
        chordsCommand.Add(chordsNoMinorSecondsOption);
        chordsCommand.Add(chordsAllowMaj7Option);
        chordsCommand.Add(chordsNoTritonesOption);
        chordsCommand.Add(chordsAllowDimOption);
        chordsCommand.Add(chordsLcmOnlyOption);
        chordsCommand.Add(chordsExplicitPlacementsOption);
        chordsCommand.Add(maxSizeOption);
        chordsCommand.Add(maxPrimeOption);
        chordsCommand.Add(maxLcmOption);
        chordsCommand.SetAction(parse =>
        {
            var k = parse.GetValue(ktetOption);
            var minNotes = parse.GetValue(chordsMinNotesOption);
            var maxNotes = parse.GetValue(chordsMaxNotesOption);
            var maxResults = parse.GetValue(chordsMaxResultsOption);
            var allowMaj7 = parse.GetValue(chordsAllowMaj7Option);
            var noMinorSeconds = parse.GetValue(chordsNoMinorSecondsOption) || allowMaj7;
            var allowDim = parse.GetValue(chordsAllowDimOption);
            var noTritones = parse.GetValue(chordsNoTritonesOption) || allowDim;
            var lcmOnly = parse.GetValue(chordsLcmOnlyOption);
            var explicitPlacements = parse.GetValue(chordsExplicitPlacementsOption);
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);

            if (k < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }
            if (k > 48) { AnsiConsole.MarkupLine("[red]--ktet must be ≤ 48 (enumeration grows combinatorially).[/]"); return 1; }
            if (minNotes < 1) { AnsiConsole.MarkupLine("[red]--min-notes must be ≥ 1.[/]"); return 1; }
            if (maxNotes < minNotes) { AnsiConsole.MarkupLine("[red]--max-notes must be ≥ --min-notes.[/]"); return 1; }
            if (minNotes > k) { AnsiConsole.MarkupLine($"[red]--min-notes {minNotes} exceeds --ktet {k}.[/]"); return 1; }
            if (maxResults < 1) { AnsiConsole.MarkupLine("[red]--max-results must be ≥ 1.[/]"); return 1; }
            if (noMinorSeconds && k != 12) { AnsiConsole.MarkupLine("[red]--no-minor-seconds / --allow-maj7 only apply to --ktet 12 (minor second / major seventh are a 12-tet concept).[/]"); return 1; }
            if (noTritones && k != 12) { AnsiConsole.MarkupLine("[red]--no-tritones / --allow-dim only apply to --ktet 12 (the tritone is a 12-tet interval).[/]"); return 1; }
            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }

            var effectiveMaxNotes = Math.Min(maxNotes, k);
            var (chords, truncated) = Chords.Enumerate(k, minNotes, effectiveMaxNotes, maxResults, noMinorSeconds, allowMaj7, noTritones, allowDim);
            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var placements = chords.Select(c => Placements.FindSupersets(c.Keys, families, k)).ToList();

            if (!explicitPlacements)
            {
                // Default: fold placements that cover the same keys (isomorphic / coincident) to
                // their lowest LCM. Independent of --lcm-only, which only checks Count > 0.
                placements = placements.Select(Placements.CollapseIsomorphic).ToList();
            }

            if (lcmOnly)
            {
                // The LCM approach: keep only necklaces that some family placement contains — the
                // same set the retired `lcm-chords` command enumerated bottom-up (proven equal).
                var kept = chords.Zip(placements, (chord, rows) => (chord, rows))
                    .Where(pair => pair.rows.Count > 0)
                    .ToList();
                chords = kept.Select(pair => pair.chord).ToList();
                placements = kept.Select(pair => pair.rows).ToList();
            }

            ChordsTableRenderer.Render(chords, placements, k, minNotes, effectiveMaxNotes, truncated, noMinorSeconds, allowMaj7, noTritones, allowDim, lcmOnly, maxLcm, explicitPlacements);
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
        tableCommand.Add(renormalizationsCommand);
        tableCommand.Add(familyOverlapCommand);
        tableCommand.Add(keySupersetsCommand);
        tableCommand.Add(superpositionsCommand);
        tableCommand.Add(chordMelodyCommand);
        tableCommand.Add(progressionCommand);
        tableCommand.Add(junctionScalesCommand);
        tableCommand.Add(voicingsCommand);
        tableCommand.Add(subsetsCommand);
        tableCommand.Add(chordsCommand);

        var modeOption = new Option<string>("--mode")
        {
            Description = "Output mode: 'full' (one node per family) or 'collapsed' (one node per iso class).",
            DefaultValueFactory = _ => "full",
        };
        modeOption.AcceptOnlyFromAmong("full", "collapsed");

        var clusterToleranceOption = new Option<double>("--cluster-tolerance")
        {
            Description = "Bin radius c allowed when snapping renormalized images to good fractions. 0 = exact only (default); 1/161 ≈ 0.00621 (the syntonic comma, = HearingConstants.MaxUniqueStandardBinRadius) admits e.g. lcm 18 → 24@10.",
            DefaultValueFactory = _ => 0.0,
        };

        var graphLcmFamiliesCommand = new Command(
            "lcm-families",
            "Emit a Mermaid graph of subset/isomorphism/renormalized-subset relationships between LCM families.");
        graphLcmFamiliesCommand.Add(maxSizeOption);
        graphLcmFamiliesCommand.Add(maxPrimeOption);
        graphLcmFamiliesCommand.Add(maxLcmOption);
        graphLcmFamiliesCommand.Add(modeOption);
        graphLcmFamiliesCommand.Add(clusterToleranceOption);
        graphLcmFamiliesCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var mode = parse.GetValue(modeOption) ?? "full";
            var clusterTolerance = parse.GetValue(clusterToleranceOption);

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
            if (clusterTolerance < 0)
            {
                AnsiConsole.MarkupLine("[red]--cluster-tolerance must be ≥ 0.[/]");
                return 1;
            }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var relations = FamilyRelations.Compute(families, fractions, clusterTolerance);
            var clustered = clusterTolerance > 0;

            string markdown;
            string fileName;
            if (mode == "collapsed")
            {
                markdown = LcmFamilyGraphRenderer.RenderCollapsed(families, relations, maxSize, maxPrime, maxLcm);
                fileName = clustered ? "lcm-families-collapsed-clustered.md" : "lcm-families-collapsed.md";
            }
            else
            {
                markdown = LcmFamilyGraphRenderer.Render(families, relations, maxSize, maxPrime, maxLcm);
                fileName = clustered ? "lcm-families-clustered.md" : "lcm-families.md";
            }

            var outputDir = Path.Combine("output", "graphs");
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, fileName);
            File.WriteAllText(outputPath, markdown);

            AnsiConsole.WriteLine(Path.GetFullPath(outputPath));
            return 0;
        });

        var placementGraphModeOption = new Option<string>("--mode")
        {
            Description = "Output mode: 'placement' (root placement + its directly adjacent supersets), 'extended' (the root's full up-set of containing placements), or 'shape' (all shapes and their 18 direct-adjacency relationships).",
            DefaultValueFactory = _ => "placement",
        };
        placementGraphModeOption.AcceptOnlyFromAmong("placement", "extended", "shape");

        var placementGraphLcmOption = new Option<int>("--lcm")
        {
            Description = "LCM of the root family for 'placement' / 'extended' modes (ignored in 'shape'). Default 1 (the tonic).",
            DefaultValueFactory = _ => 1,
        };
        var placementGraphAtOption = new Option<int>("--at")
        {
            Description = "Anchor key of the root placement for 'placement' / 'extended' modes (ignored in 'shape'). Default 0.",
            DefaultValueFactory = _ => 0,
        };
        var placementGraphShowIsomorphismsOption = new Option<bool>("--show-isomorphisms")
        {
            Description = "In 'placement' / 'extended' modes, append each node's keyboard-coincident (isomorphic) placements with `≅` (e.g. 4@8 ≅ 3@3). Default off — each node shows a single representative. Ignored in 'shape' mode.",
        };

        var graphPlacementCommand = new Command(
            "placement",
            "Emit a Mermaid placement graph: a placement and its directly adjacent placements (cover relation over k-tet key-set containment), the root's full up-set, or the shape-collapsed graph of all direct-adjacency relationships.");
        graphPlacementCommand.Add(maxSizeOption);
        graphPlacementCommand.Add(maxPrimeOption);
        graphPlacementCommand.Add(maxLcmOption);
        graphPlacementCommand.Add(ktetOption);
        graphPlacementCommand.Add(placementGraphModeOption);
        graphPlacementCommand.Add(placementGraphLcmOption);
        graphPlacementCommand.Add(placementGraphAtOption);
        graphPlacementCommand.Add(placementGraphShowIsomorphismsOption);
        graphPlacementCommand.SetAction(parse =>
        {
            var maxSize = parse.GetValue(maxSizeOption);
            var maxPrime = parse.GetValue(maxPrimeOption);
            var maxLcm = parse.GetValue(maxLcmOption);
            var k = parse.GetValue(ktetOption);
            var mode = parse.GetValue(placementGraphModeOption) ?? "placement";
            var lcm = parse.GetValue(placementGraphLcmOption);
            var at = parse.GetValue(placementGraphAtOption);
            var showIsomorphisms = parse.GetValue(placementGraphShowIsomorphismsOption);

            if (maxSize < 1) { AnsiConsole.MarkupLine("[red]--max-size must be ≥ 1.[/]"); return 1; }
            if (maxPrime < 2) { AnsiConsole.MarkupLine("[red]--max-prime must be ≥ 2.[/]"); return 1; }
            if (maxLcm < 1) { AnsiConsole.MarkupLine("[red]--max-lcm must be ≥ 1.[/]"); return 1; }
            if (k < 1) { AnsiConsole.MarkupLine("[red]--ktet must be ≥ 1.[/]"); return 1; }

            var fractions = GoodFractions.Enumerate(maxSize, maxPrime);
            var families = LcmFamilies.Compute(fractions, maxLcm);
            var nodes = PlacementGraph.BuildUniverse(families, k);
            var edges = PlacementGraph.CoverEdges(nodes);

            string markdown;
            string fileName;
            if (mode == "shape")
            {
                var (shapes, shapeEdges) = PlacementGraph.ShapeGraph(nodes, edges, k);
                markdown = PlacementGraphRenderer.RenderShapeGraph(shapes, shapeEdges, maxSize, maxPrime, maxLcm, k);
                fileName = "placement-shape.md";
            }
            else
            {
                var rootFamily = families.FirstOrDefault(f => f.Lcm == lcm);
                if (rootFamily.Fractions is null)
                {
                    AnsiConsole.MarkupLine($"[red]No LCM family {lcm} within --max-lcm {maxLcm}.[/]");
                    return 1;
                }
                var normalizedAt = ((at % k) + k) % k;
                var rootKeys = Placements.Compute(rootFamily, normalizedAt, k).Keys;
                var rootIndex = PlacementGraph.FindNode(nodes, rootKeys);
                if (rootIndex < 0)
                {
                    AnsiConsole.MarkupLine($"[red]Could not locate placement {lcm}@{normalizedAt} in the universe.[/]");
                    return 1;
                }

                if (mode == "extended")
                {
                    var scope = PlacementGraph.UpSet(nodes, rootKeys);
                    markdown = PlacementGraphRenderer.RenderScoped(
                        $"Extended Placement Graph — {lcm}@{normalizedAt}",
                        nodes, edges, scope, maxSize, maxPrime, maxLcm, k, showIsomorphisms);
                    fileName = $"placement-extended-{lcm}@{normalizedAt}.md";
                }
                else
                {
                    var scope = PlacementGraph.PlacementScope(edges, rootIndex);
                    markdown = PlacementGraphRenderer.RenderScoped(
                        $"Placement Graph — {lcm}@{normalizedAt}",
                        nodes, edges, scope, maxSize, maxPrime, maxLcm, k, showIsomorphisms);
                    fileName = $"placement-{lcm}@{normalizedAt}.md";
                }
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
        graphCommand.Add(graphPlacementCommand);

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
