# Melodroid 3

A console application for **music research from first principles**: exploring music as the meeting point between the **physics of sound** (origin) and the **biology of human hearing** (destination).

The program is exploratory — it produces artifacts you inspect, not a polished user-facing app. Current output modes:

- **Console tables** — Spectre.Console renderings of good fractions, LCM families, and their relations.
- **Mermaid graphs** — relation graphs between LCM families (literal subset / isomorphism / renormalized subset), written as `.md` for in-editor preview.
- **PNG plots** — ScottPlot waveform plots of an LCM family's constituent sines and their superposition.
- **MIDI files** *(planned, not yet implemented)* — for auditioning musical ideas. `Melanchall.DryWetMidi` will be added when the first MIDI writer lands.

For the full theoretical premise (good fractions, wave pattern length, renormalization, isomorphism examples), see [README.md](README.md). CLAUDE.md is the operating manual; README is the theory document.

## Tech stack

- **.NET 9.0** / **C# 12** — see [Melodroid 3.csproj](Melodroid%203.csproj)
- Implicit usings and nullable reference types are both **enabled**

### Libraries

Add new packages on demand via `dotnet add package <Name>` — don't pre-install.

| Purpose | Package | Status |
| --- | --- | --- |
| Rich console tables, prompts, colour | `Spectre.Console` | installed |
| Headless PNG plot generation | `ScottPlot` | installed |
| Subcommand-style CLI parsing | `System.CommandLine` | installed (preview) |
| Testing | `xUnit` + `FluentAssertions` | installed (in `Melodroid.Tests/`) |
| MIDI read/write/generation | `Melanchall.DryWetMidi` | planned |

## Project structure

The four-module backbone (`Physics`, `Hearing`, `Music`, `Output`) is stable. New folders should still be added on demand, but expect most new code to slot into an existing module.

```
Melodroid 3/
├── Program.cs                # CLI entry + System.CommandLine wiring
├── src/
│   ├── Physics/              # Sine.cs — pure waveform sampling
│   ├── Hearing/              # HearingConstants.cs — psychoacoustic thresholds
│   ├── Music/                # Fraction, GoodFractions, LcmFamilies,
│   │                         #   FamilyRelations, Renormalization
│   └── Output/               # Table / graph / waveform renderers
├── Melodroid.Tests/          # xUnit + FluentAssertions; mirrors src/ layout
└── output/                   # Generated artifacts (gitignored)
    ├── graphs/               # Mermaid .md files
    └── plots/                # ScottPlot .png files
```

## CLI conventions

Use `System.CommandLine` with subcommands grouped by output type. Current command surface:

```
dotnet run -- table good-fractions   [--max-size 24] [--max-prime 5]
dotnet run -- table lcm-families     [--max-size 24] [--max-prime 5] [--max-lcm 24]
dotnet run -- graph lcm-families     [--max-size 24] [--max-prime 5] [--max-lcm 24] [--mode full|collapsed]
dotnet run -- plot  lcm-families --lcm N
                                     [--max-size 24] [--max-prime 5]
                                     [--samples-per-period 200] [--mode all|sum|constituents]
                                     [--subset-lcm K]
dotnet run -- table placement       --lcm L [L...] --at B --ktet K
                                     [--max-size 24] [--max-prime 5] [--max-lcm 24]
dotnet run -- table family-overlap  --lcm-sweep A --lcm-ref B --ktet K
                                     [--max-size 24] [--max-prime 5] [--max-lcm 24]
dotnet run -- table key-supersets   --keys 0 4 7 --ktet K
                                     [--max-size 24] [--max-prime 5] [--max-lcm 24]
```

Each subcommand writes its result under `output/<category>/` and prints the path to stdout. See [README.md](README.md) for what each command produces and how to interpret it.

When adding or changing a CLI command (new subcommand, new option, behaviour change), update [README.md](README.md) alongside the code change. Document the command under the most-relevant theory section (Good Fractions, LCM Families, Isomorphisms and Subsets, Wave Pattern Plots) so the theory and the command that exposes it stay co-located.

## Build / run / test

| Action | Command |
| --- | --- |
| Build | `dotnet build` |
| Run | `dotnet run -- <subcommand>` |
| Test | `dotnet test` |
| Add a package | `dotnet add package <Name>` |

## Output organisation

- All generated artifacts go under `output/` at the repo root.
- `output/` is **gitignored** — research artifacts are reproducible from code, not committed.
- One subdirectory per type: currently `output/graphs/`, `output/plots/`. `output/midi/` will appear when MIDI generation lands.

## Code conventions

- Nullable reference types are enabled — annotate properly, don't suppress without reason.
- **Pure functions** for physics/math (input → output, no side effects). They're trivial to test and to plot.
- Keep **domain code** (physics, hearing, music theory) **free of I/O**. MIDI/table/plot writers live in `src/Output/`.
- Put **units in names** where ambiguity matters: `frequencyHz`, `durationMs`, `amplitudeDb`, `phaseRad`.
- **Static utility classes** for domain logic — `GoodFractions`, `LcmFamilies`, `FamilyRelations`, `Renormalization`, `Sine` are all static, stateless, and called as `Type.Method(...)`. New domain operations follow the same shape.
- **Immutable data** — domain types are `readonly record struct` (`Fraction`, `LcmFamily`, `FamilyRelation`). Prefer the same for new value types.
- **Namespace** is `Melodroid_3` (underscore, matches the .csproj `RootNamespace`) — not `Melodroid.3` or `Melodroid3`.
- **Input validation lives in [Program.cs](Program.cs)**, not in domain code. The CLI layer surfaces errors via `AnsiConsole.MarkupLine("[red]...[/]")` and returns a non-zero exit code; domain methods assume valid inputs.
- **Hasse reduction of transitive edges** is a recurring pattern in graph rendering and family-relation computation — reuse the existing helpers in [src/Output/LcmFamilyGraphRenderer.cs](src/Output/LcmFamilyGraphRenderer.cs) and [src/Music/FamilyRelations.cs](src/Music/FamilyRelations.cs) rather than re-deriving the logic.
- **Tests mirror `src/`** — `Melodroid.Tests/Music/`, `Melodroid.Tests/Output/`. Use `[Theory] + [InlineData]` for property-style coverage. Do **not** DRY math predicates (`Gcd`, `IsSmooth`, `IsPrime`) into a shared test helper — the reimplementation in tests is the independent oracle.

## Domain anchors

The *why*, so the codebase doesn't drift away from its research purpose.

**Core thesis** — harmony as **pattern recognition of simultaneous waves**. A chord expressed as fractions of a reference frequency (e.g. a major triad as `{1, 5/4, 3/2}`) produces a superposition whose **wave pattern length** (WPL) is the LCM of the denominators, in units of the reference period.

**Hearing constraint** — the lower bound of human pitch perception (≈ 20 Hz) implies a pattern lasting much longer than ~50 ms is unlikely to be recognised as a unit. See [src/Hearing/HearingConstants.cs](src/Hearing/HearingConstants.cs). Note: two constants share the value 50 ms but mean different things — `MinAudibleFrequencyHz` (period of 20 Hz) is the pattern-length ceiling, while `ActionToSoundLatencyJndMs` is the key-press → sound latency JND. Don't conflate them.

**Good fractions** — numerator and denominator both ≤ `maxSize`, factored from primes ≤ `maxPrime` (defaults 24 / 5). Octave-equivalent: mapped to `[1, 2)`. Implemented in [src/Music/GoodFractions.cs](src/Music/GoodFractions.cs).

**LCM families** — the maximal subset of good fractions whose denominators have a given LCM (= the WPL for that subset). Implemented in [src/Music/LcmFamilies.cs](src/Music/LcmFamilies.cs).

**Family relations** — three kinds of edges between families: **literal subset**, **isomorphism** (via renormalization to a new base fraction), and **renormalized subset** (isomorphic to a proper subset of a larger family). Implemented in [src/Music/FamilyRelations.cs](src/Music/FamilyRelations.cs) with renormalization in [src/Music/Renormalization.cs](src/Music/Renormalization.cs).

**What's next** — the original research compass remains aspirational and may guide future work: Fourier decomposition, wavepackets, beat frequencies, dissonance from interference (physics side); cochlear frequency mapping, critical bands, equal-loudness contours, broader JNDs, consonance perception (biology side). These are directions, not current scope.

A good change usually:

- connects something on the physics side to something on the biology side, **or**
- makes one of those easier to explore via table, graph, plot, or (eventually) MIDI.
