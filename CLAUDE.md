# Melodroid 3

A program for **music research from first principles**: exploring music as the meeting point between the **physics of sound** (origin) and the **biology of human hearing** (destination). Melodroid is mainly a CLI program producing various plots and printouts for study, with a companion website under [website/](website/) that works as docs and explains the theory.

The program is exploratory — it produces artifacts you inspect, not a polished user-facing app. Current output modes:

- **Console tables** — Spectre.Console renderings of good fractions, LCM families, their relations, JND-bin overlaps, k-tet covering radii, ratio sweeps, placements, and voicings.
- **Mermaid graphs** — relation graphs between LCM families (literal subset / isomorphism / renormalized subset), written as `.md` for in-editor preview.
- **PNG plots** — ScottPlot waveform plots of an LCM family's constituent sines and their superposition.
- **Companion website** — a Docusaurus docs site under [website/](website/) that explains the theory, with interactive Tone.js audio (chords / voicings / families) and interactive wave plots. Deployed to GitHub Pages.
- **MIDI files** *(planned, not yet implemented)* — for auditioning musical ideas. `Melanchall.DryWetMidi` will be added when the first MIDI writer lands.

For the full theoretical premise (good fractions, wave pattern length, renormalization, isomorphism examples), see the docs under [website/docs/](website/docs/). CLAUDE.md is the operating manual; the website is the theory documents.

## Tech stack

- **.NET 9.0** / **C# 12** — see [Melodroid 3.csproj](Melodroid%203.csproj). The CLI assembly is named `melodroid`.
- Implicit usings and nullable reference types are both **enabled**

### Libraries (CLI)

Add new packages on demand via `dotnet add package <Name>` — don't pre-install.

| Purpose | Package | Status |
| --- | --- | --- |
| Rich console tables, prompts, colour | `Spectre.Console` | installed |
| Headless PNG plot generation | `ScottPlot` | installed |
| Subcommand-style CLI parsing | `System.CommandLine` | installed (preview) |
| Testing | `xUnit` + `FluentAssertions` | installed (in `Melodroid.Tests/`) |
| MIDI read/write/generation | `Melanchall.DryWetMidi` | planned |

### Website

The companion docs site under [website/](website/) is **Docusaurus + TypeScript**. It uses **Tone.js** for Web Audio playback, **KaTeX** (`remark-math` / `rehype-katex`) for typeset math, and `@docusaurus/theme-mermaid` for native Mermaid rendering. Run site commands from `website/` (`npm run build`, `npm run start`); Node 24 is required. The C# CLI stays the source of truth for the underlying data.

## Project structure

The four-module backbone (`Physics`, `Hearing`, `Music`, `Output`) is stable. New folders should still be added on demand, but expect most new code to slot into an existing module.

```
Melodroid 3/
├── Program.cs                # CLI entry + System.CommandLine wiring
├── src/
│   ├── Physics/              # Sine.cs — pure waveform sampling
│   ├── Hearing/              # HearingConstants.cs — psychoacoustic thresholds
│   ├── Music/                # Domain logic: Fraction, GoodFractions, LcmFamilies,
│   │                         #   FamilyRelations, Renormalization; keyboard thrust:
│   │                         #   BinOverlaps, KeysNeeded, OctaveSweep, KeySweep,
│   │                         #   Placements, Voicings; shared math: IntegerMath, RatioMath
│   └── Output/               # One renderer per table / graph / plot type
├── Melodroid.Tests/          # xUnit + FluentAssertions; mirrors src/ layout
├── website/                  # Docusaurus docs site (TypeScript)
│   ├── docs/                 #   theory/ · keyboard/ · music/ · cli/ (each with _category_.json)
│   ├── src/                  #   components/ (Player, VoicingPlayer, WavePlot) · lib/
│   └── static/img/plots/     #   PNGs referenced from the docs
└── output/                   # Generated artifacts (gitignored)
    ├── graphs/               # Mermaid .md files
    └── plots/                # ScottPlot .png files
```

## CLI conventions

Use `System.CommandLine` with subcommands grouped by output type. Each subcommand writes its result under `output/<category>/` and prints the path to stdout. Most commands share the good-fraction options (`--max-size` 24, `--max-prime` 5, `--max-lcm` 24, `--ktet` 12).

The shipped binary is invoked as `melodroid <subcommand>`; from a clone it's `dotnet run -- <subcommand>`. Command index (one line each):

```
table good-fractions    enumerate good fractions in [1, 2)
table lcm-families      max-sized good-fraction subset per LCM (wave pattern length)
table bin-overlaps      bin radius c where adjacent fractions' JND clusters touch
table octave-sweep      bin --ratios against good fractions over a swept reference octave
table ktet-cutoffs      worst-case covering radius c_k per k-tet keyboard
table key-sweep         bin --keys / --ratios stepping through a k-tet tuning's keys
table placement         map an LCM family's fractions onto k-tet keys at a chosen anchor
table family-overlap    sweep one family's placements against a reference family
table key-supersets     placements whose keys are a superset of given --keys
table voicings          lowest-penalty ascending voicings (--lcm or --keys)
table chord-melody      matrix of maximal-LCM placements containing a chord vs each key
graph lcm-families      Mermaid relation graph (--mode full|collapsed)
plot  lcm-families      ScottPlot waveform (--mode all|sum|constituents|difference, --subset-lcm)
```

The authoritative full flag surface lives in [website/docs/cli/reference.mdx](website/docs/cli/reference.mdx) — keep it in sync when adding or changing commands.

## Build / run / test

| Action | Command |
| --- | --- |
| Build | `dotnet build` |
| Run | `dotnet run -- <subcommand>` |
| Test | `dotnet test` |
| Add a package | `dotnet add package <Name>` |
| Build docs site | `cd website && npm run build` |
| Run docs dev server | `cd website && npm run start` |

## Output organisation

- All generated artifacts go under `output/` at the repo root.
- `output/` is **gitignored** — research artifacts are reproducible from code, not committed.
- One subdirectory per type: currently `output/graphs/`, `output/plots/`. `output/midi/` will appear when MIDI generation lands.

## Code conventions

- Nullable reference types are enabled — annotate properly, don't suppress without reason.
- **Pure functions** for physics/math (input → output, no side effects). They're trivial to test and to plot.
- Keep **domain code** (physics, hearing, music theory) **free of I/O**. MIDI/table/plot writers live in `src/Output/`.
- Put **units in names** where ambiguity matters: `frequencyHz`, `durationMs`, `amplitudeDb`, `phaseRad`.
- **Static utility classes** for domain logic — `GoodFractions`, `LcmFamilies`, `FamilyRelations`, `Renormalization`, `BinOverlaps`, `KeysNeeded`, `OctaveSweep`, `KeySweep`, `Placements`, `Voicings`, `Sine` are all static, stateless, and called as `Type.Method(...)`. New domain operations follow the same shape.
- **No production math duplication** — shared integer math lives in [src/Music/IntegerMath.cs](src/Music/IntegerMath.cs) (`Gcd`, `Lcm`) and ratio math in [src/Music/RatioMath.cs](src/Music/RatioMath.cs) (`OctaveNormalize`, `CircularSignedRelative`). Domain code calls these rather than re-deriving them. (The tests-only exception below is deliberate.)
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

**Cluster ranges / JND bins** — each good fraction owns a perceptual bin; for adjacent fractions there is a bin radius *c* at which their JND clusters first overlap (the binding constraint on how finely fractions can be distinguished). Implemented in [src/Music/BinOverlaps.cs](src/Music/BinOverlaps.cs); see docs `theory/cluster-ranges`.

**k-tet keyboards & covering radius cₖ** — for a *k*-key equal-tempered keyboard, the worst-case multiplicative error against the good fractions; the smallest *c* at which *k* keys cover every good fraction. This drives the "optimal number of keys" question. Implemented in [src/Music/KeysNeeded.cs](src/Music/KeysNeeded.cs); see docs `keyboard/a-good-keyboard`.

**Sweeps** — bin renormalized input ratios against the good fractions, either across a swept reference octave ([src/Music/OctaveSweep.cs](src/Music/OctaveSweep.cs)) or stepping through the *k* keys of a tuning ([src/Music/KeySweep.cs](src/Music/KeySweep.cs)); see docs `keyboard/key-sweep`.

**Placements & voicings** — a **placement** maps an LCM family's fractions onto *k*-tet keys anchored on a chosen key ([src/Music/Placements.cs](src/Music/Placements.cs), used by `placement`, `family-overlap`, `key-supersets`, `chord-melody`). **Voicings** are ascending, semitone-avoiding orderings of a key set, lowest-penalty per root ([src/Music/Voicings.cs](src/Music/Voicings.cs)); see docs `music/voicings-and-placements`.

**What's next** — the original research compass remains aspirational and may guide future work: Fourier decomposition, wavepackets, beat frequencies, dissonance from interference (physics side); cochlear frequency mapping, critical bands, equal-loudness contours, broader JNDs, consonance perception (biology side). These are directions, not current scope.

A good change usually:

- connects something on the physics side to something on the biology side, **or**
- makes one of those easier to explore via table, graph, plot, or (eventually) MIDI.
