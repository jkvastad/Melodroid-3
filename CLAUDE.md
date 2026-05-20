# Melodroid 3

A console application for **music research from first principles**: exploring music as the meeting point between the **physics of sound** (origin) and the **biology of human hearing** (destination).

The program is exploratory — it produces artifacts you inspect, not a polished user-facing app. Three output modes:

- **MIDI files** — procedurally generated, for auditioning musical ideas
- **Console tables** — side-by-side comparison of key data (frequencies, intervals, perceptual thresholds, etc.)
- **Graphs** — visualisations of wavepackets, spectra, and other waveform data

## Tech stack

- **.NET 9.0** / **C# 12** — see [Melodroid 3.csproj](Melodroid%203.csproj)
- Implicit usings and nullable reference types are both **enabled**

### Recommended libraries

Add via `dotnet add package <Name>` when first needed — do not pre-install.

| Purpose | Package |
| --- | --- |
| MIDI read/write/generation | `Melanchall.DryWetMidi` |
| Rich console tables, prompts, colour | `Spectre.Console` |
| Headless PNG plot generation | `ScottPlot` |
| Subcommand-style CLI parsing | `System.CommandLine` |
| Testing | `xUnit` + `FluentAssertions` |

## Project structure

Greenfield — the layout will grow on demand. Target organisation once code spreads beyond [Program.cs](Program.cs):

```
Melodroid 3/
├── Program.cs              # CLI entry + command wiring
├── src/
│   ├── Physics/            # Wave math, harmonics, Fourier, wavepackets
│   ├── Hearing/            # Psychoacoustics: critical bands, masking, pitch perception
│   ├── Music/              # Scales, intervals, MIDI generation
│   └── Output/             # MIDI writers, table renderers, plot renderers
├── Melodroid.Tests/        # xUnit tests (create when first test is written)
└── output/                 # Generated artifacts — gitignored
    ├── midi/
    └── plots/
```

Do **not** pre-create empty folders. Add them when the first file needs them.

## CLI conventions

Use `System.CommandLine` with subcommands grouped by output type:

```
dotnet run -- midi generate-scale --root A4 --mode dorian
dotnet run -- table compare-tunings
dotnet run -- plot wavepacket --carrier 440 --envelope gaussian
```

Each subcommand writes its result under `output/<category>/` and prints the path to stdout.

## Build / run / test

| Action | Command |
| --- | --- |
| Build | `dotnet build` |
| Run | `dotnet run -- <subcommand>` |
| Test | `dotnet test` (once `Melodroid.Tests/` exists) |
| Add a package | `dotnet add package <Name>` |

## Output organisation

- All generated artifacts go under `output/` at the repo root.
- `output/` is **gitignored** — research artifacts are reproducible from code, not committed.
- One subdirectory per type: `output/midi/`, `output/plots/`, etc.

## Code conventions

- Nullable reference types are enabled — annotate properly, don't suppress without reason.
- **Pure functions** for physics/math (input → output, no side effects). They're trivial to test and to plot.
- Keep **domain code** (physics, hearing, music theory) **free of I/O**. MIDI/table/plot writers live in `src/Output/`.
- Put **units in names** where ambiguity matters: `frequencyHz`, `durationMs`, `amplitudeDb`, `phaseRad`.

## Domain anchors

The *why*, so the codebase doesn't drift away from its research purpose.

**Physics side** — sinusoids, harmonics, Fourier decomposition, wavepackets, beat frequencies, dissonance from interference.

**Biology side** — cochlear frequency mapping, critical bands, equal-loudness contours, just-noticeable differences, consonance perception.

A good change usually:

- connects something on the physics side to something on the biology side, **or**
- makes one of those easier to explore via MIDI, table, or plot.
