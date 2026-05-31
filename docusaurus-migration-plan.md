# Melodroid-3 → Docusaurus Migration Plan

Goal: move the theory currently living in `README.md` into a structured Docusaurus
site under `website/`, with LaTeX math, native Mermaid graphs, and interactive
Web Audio playback of LCM families / voicings. Keep the C# CLI as the source of
truth for data.

This plan is written to be worked through with the Claude VS Code extension. Each
phase is small enough to hand off as a single task ("do Phase 2", etc.) and verify
before moving on.

---

## Target repo layout

```
Melodroid-3/
├─ src/                     # C# (unchanged)
├─ Melodroid.Tests/         # C# tests (unchanged)
├─ Program.cs               # CLI entry (unchanged)
├─ output/                  # generated plots/graphs (unchanged)
├─ README.md                # SHRINKS → short intro + link to docs site
└─ website/                 # NEW: Docusaurus site
   ├─ docs/                 # the migrated theory, as MDX pages
   ├─ src/
   │  ├─ components/        # VoicingPlayer, etc.
   │  └─ data/              # JSON exported from the C# tool (optional)
   ├─ static/
   ├─ docusaurus.config.js
   └─ sidebars.js
```

Keeping the site in a `website/` subfolder (rather than its own repo) means the
docs version-control alongside the code that produces the data, and GitHub Pages
can deploy from the same repo.

---

## Phase 0 — Prerequisites & decisions

- [ ] Install Node 18+ (`node -v`). Docusaurus 3 needs it; your `dotnet` toolchain is untouched.
- [ ] Decide the GitHub Pages URL. For a project repo it will be
      `https://jkvastad.github.io/Melodroid-3/` → so `baseUrl: '/Melodroid-3/'`.
- [ ] Add to root `.gitignore`:
      ```
      website/node_modules/
      website/build/
      website/.docusaurus/
      ```

---

## Phase 1 — Scaffold the site

- [ ] From repo root:
      ```bash
      npx create-docusaurus@latest website classic --typescript
      cd website
      npm run start          # http://localhost:3000, hot reload
      ```
- [ ] Strip the demo content: delete the blog (you likely don't need it — remove the
      blog preset option in `docusaurus.config.js`) and the tutorial pages under `docs/`.
- [ ] Set `title`, `tagline` ("A program for exploring music"), `url`, `baseUrl`,
      `organizationName: 'jkvastad'`, `projectName: 'Melodroid-3'`.

**Checkpoint:** blank site builds and serves locally.

---

## Phase 2 — Enable math + Mermaid

These two features are the main reason Docusaurus beats the README for your content.

- [ ] Install:
      ```bash
      npm install remark-math rehype-katex @docusaurus/theme-mermaid
      ```
- [ ] In `docusaurus.config.js`:
      - add `markdown: { mermaid: true }`
      - add `themes: ['@docusaurus/theme-mermaid']`
      - on the docs preset, add `remarkPlugins: [remarkMath]` and
        `rehypePlugins: [rehypeKatex]`
      - add the KaTeX stylesheet under `stylesheets` (the KaTeX CSS CDN link with its
        integrity hash — copy the current one from the Docusaurus math docs).
- [ ] Smoke-test one of each on a scratch page:
      - Inline math: `$c = \frac{b-a}{b+a}$`
      - A fenced ` ```mermaid ` block with a tiny `graph TD; A-->B`.

**Checkpoint:** a formula renders typeset, a Mermaid graph renders inline.

---

## Phase 3 — Migrate the README content

Your README already has a clean heading hierarchy; it maps almost 1:1 onto a docs
tree. Suggested split (one `.mdx` file per leaf, grouped in folders with a
`_category_.json` for sidebar labels):

```
docs/
├─ intro.mdx                         # "Melodroid 3" + Theoretical premise
├─ theory/
│  ├─ good-fractions.mdx
│  ├─ lcm-families.mdx               # incl. Isomorphisms & Subsets
│  ├─ wave-pattern-plots.mdx
│  ├─ cluster-ranges.mdx             # incl. Octave Sweep, Centered Full Matches
│  └─ playing-fractions.mdx
├─ keyboard/
│  ├─ a-good-keyboard.mdx            # Modulation / Full Expression / Minimal Complexity
│  └─ key-sweep.mdx
├─ music/
│  ├─ voicings-and-placements.mdx
│  ├─ voicings-and-lcm-families.mdx  # incl. Chords and Melody
│  └─ on-a-sour-note.mdx             # (your "Deferred" section)
└─ cli/
   └─ reference.mdx                  # all `dotnet run -- ...` commands in one place
```

Migration steps:

- [ ] Move prose section-by-section. Don't try to do it all at once — one file per
      task is a good unit to hand to Claude Code.
- [ ] **Convert inline-code math to LaTeX.** Things currently written as
      `` `c = (b-a)/(b+a)` `` and `` `c_k = |2^(n/k) − g_k*| / g_k*` `` become real
      formulas. Watch for the Unicode (`≥`, `·`, `²`, `−` minus vs hyphen) — translate
      to proper LaTeX (`\geq`, `\cdot`, `^2`, `-`).
- [ ] **Convert the box-drawing ASCII tables** (the `┌─┬─┐` k-tet cutoff table and the
      LCM→chord-name table) into real Markdown tables. Bonus: once Phase 5 is done you
      can regenerate these from the tool instead of hand-maintaining them.
- [ ] **Replace the "open in VS Code, press Ctrl+Shift+V" Mermaid instruction** with the
      actual rendered graph: paste the output of `dotnet run -- graph lcm-families` into a
      ` ```mermaid ` block on `lcm-families.mdx`.
- [ ] **Plots:** for now, commit the PNGs from `output/plots/` into
      `static/img/plots/` and reference them. (Later you could render them client-side, but
      static images are fine to start.)
- [ ] **CLI reference:** collect every `dotnet run -- ...` invocation and its options
      into `cli/reference.mdx`. This is the part that's most painful in one long README and
      most improved by being its own searchable section.
- [ ] Split the long research-note paragraphs (the "On a Sour Note" / preference /
      uniform-chords musings) into clearly-marked notes using Docusaurus admonitions
      (`:::note`, `:::tip`, `:::caution`).

**Checkpoint:** the README's content is fully represented across the docs tree; the
sidebar reads like a table of contents for the theory.

---

## Phase 4 — Interactive audio playback

This is the payoff that the README literally can't do (your TODO:
"web app for listening to the voicings"). MDX lets you embed a React component on any
page.

Approach: use **Tone.js** from the start. It wraps the Web Audio API with synths,
envelopes, a transport/scheduler, and effects — which is what you'll want for the more
advanced audio later (sequencing melodies over chords, arpeggiation, per-note timing for
the "chords and melody" experiments). A `PolySynth` plays arbitrary frequencies in Hz,
so your fraction sets and 12-tet ratios drop straight in without note-name conversion.

- [ ] Install Tone.js (scoped to the site):
      ```bash
      cd website && npm install tone
      ```

- [ ] Create `src/components/VoicingPlayer.tsx`. Minimal Tone.js version:

      ```tsx
      import React, { useEffect, useRef, useState } from 'react';
      import * as Tone from 'tone';

      type Props = {
        // either fractions on [1,2) ...
        fractions?: number[];
        // ... or 12-tet key indices, converted to ratios 2^(n/k)
        keys?: number[];
        ktet?: number;                                  // default 12
        fundamental?: number;                           // Hz, default 220
        label?: string;
        mode?: 'chord' | 'arpeggio';                    // default 'chord'
        oscillator?: 'sine' | 'triangle' | 'square' | 'sawtooth'; // default 'sine'
      };

      export default function VoicingPlayer({
        fractions, keys, ktet = 12, fundamental = 220,
        label = 'Play', mode = 'chord', oscillator = 'sine',
      }: Props) {
        const synthRef = useRef<Tone.PolySynth | null>(null);
        const [playing, setPlaying] = useState(false);

        const ratios = fractions ?? (keys ?? []).map((n) => Math.pow(2, n / ktet));
        const freqs = ratios.map((r) => fundamental * r);

        // build the synth lazily in the browser; dispose on unmount
        const getSynth = () => {
          if (!synthRef.current) {
            synthRef.current = new Tone.PolySynth(Tone.Synth, {
              oscillator: { type: oscillator },         // pure sine = your wave-pattern model
              envelope: { attack: 0.02, decay: 0.1, sustain: 0.8, release: 0.4 },
              volume: -12,                              // headroom; PolySynth sums voices
            }).toDestination();
          }
          return synthRef.current;
        };
        useEffect(() => () => synthRef.current?.dispose(), []);

        const play = async () => {
          await Tone.start();                           // unlock audio on user gesture
          const synth = getSynth();
          if (mode === 'arpeggio') {
            const t0 = Tone.now();
            freqs.forEach((f, i) => synth.triggerAttackRelease(f, '8n', t0 + i * 0.25));
          } else {
            synth.triggerAttack(freqs);                 // sustained block chord
            setPlaying(true);
          }
        };

        const stop = () => {
          synthRef.current?.releaseAll();
          setPlaying(false);
        };

        return (
          <button onClick={playing ? stop : play}>
            {playing ? 'Stop' : label}
          </button>
        );
      }
      ```

- [ ] Use it inside any `.mdx` page. Tone touches browser-only APIs, so wrap it in
      Docusaurus's `<BrowserOnly>` to keep the static build (SSR) happy:

      ```mdx
      import BrowserOnly from '@docusaurus/BrowserOnly';

      The major chord {1, 5/4, 3/2}:

      <BrowserOnly>
        {() => {
          const VoicingPlayer = require('@site/src/components/VoicingPlayer').default;
          return <VoicingPlayer fractions={[1, 1.25, 1.5]} label="Play 4@0" />;
        }}
      </BrowserOnly>
      ```

      (If you'll use the player on many pages, wrap this boilerplate once in a small
      `<Player .../>` wrapper component so each page is just `<Player fractions={...} />`.)

- [ ] Follow-ups that Tone.js makes cheap (do later): a `mode="arpeggio"` toggle (already
      stubbed above), a fundamental slider so readers hear the WPD difference across the
      100–1000 Hz range, a "play the superposition sum" mode mirroring your `plot --mode sum`,
      and — once you sequence melody over chords — `Tone.Transport` + `Tone.Part` to schedule
      the "chords and melody" examples with real timing, plus a reverb/filter on the output bus.

**Checkpoint:** clicking a button on the voicings page sounds the chord via Tone.js.

---

## Phase 5 — Connect the C# tool as the data source (optional, high value)

Right now the README hand-lists fraction sets, voicings, and tables. To avoid those
drifting out of sync with the code, make the C# tool emit JSON the site imports.

- [ ] Add a CLI command, e.g. `dotnet run -- export json --out website/src/data/`,
      that writes things like `lcm-families.json`, `voicings.json`,
      `ktet-cutoffs.json`.
- [ ] Import that JSON in MDX/components to (a) auto-render the tables you currently
      keep as ASCII art, and (b) feed `VoicingPlayer` real fraction sets instead of
      hardcoded numbers.
- [ ] Optionally add an npm script that shells out to dotnet before build so the data
      is always fresh:
      ```json
      "scripts": { "predata": "cd .. && dotnet run -- export json --out website/src/data/" }
      ```

This makes the docs a *view* over the model rather than a parallel copy of it.

---

## Phase 6 — Deploy to GitHub Pages

- [ ] Add a GitHub Actions workflow (`.github/workflows/deploy-docs.yml`) that, on push
      to `master`: sets up Node, optionally runs the dotnet export from Phase 5,
      `npm ci && npm run build` inside `website/`, and publishes `website/build` to Pages.
- [ ] Enable Pages in repo settings (source: GitHub Actions).
- [ ] Confirm `baseUrl: '/Melodroid-3/'` so asset paths resolve under the project path.

**Checkpoint:** the site is live at `jkvastad.github.io/Melodroid-3/` and updates on push.

---

## Phase 7 — Your new authoring workflow

Previously: all theory went into one `README.md`. New normal:

- **Where theory lives:** the relevant `docs/**/*.mdx` page, not the README. The README
  shrinks to a short intro + build instructions + a link to the site.
- **Math:** write it as LaTeX (`$...$` inline, `$$...$$` block) instead of inline code.
- **Diagrams:** paste Mermaid into a ` ```mermaid ` fence; it renders in the page. When you
  change the graph generator, re-paste (or, post-Phase 5, generate into a JSON/MDX file).
- **New CLI command:** document it on `cli/reference.mdx` in the same PR that adds it.
- **Hearing an idea:** drop a `<VoicingPlayer .../>` next to the prose that introduces a
  chord/family so the claim is audible, not just described.
- **Local loop:** `npm run start` gives live reload while you write — much tighter than
  the README + VS Code preview cycle.
- **With the Claude VS Code extension:** good task-sized prompts are "migrate the
  Cluster Ranges section of README.md into docs/theory/cluster-ranges.mdx, converting the
  inline-code formulas to KaTeX and the ASCII table to a Markdown table", or "add an
  arpeggiate toggle and a 50ms attack/release to VoicingPlayer.tsx". Hand off one phase or
  one section at a time and verify the local build between steps.
- **Drift control:** treat the C# tool as the source of truth. If a number appears in both
  the code and the docs, prefer generating it (Phase 5) over typing it twice.

---

## Suggested order of execution

Phases 1 → 2 → 3 give you a working, better-organized docs site (the bulk of the win).
Phase 4 adds the audio you specifically want. Phases 5–6 are the "do it properly"
finish. You can ship after Phase 4 and add 5–6 later without rework.

## Gotchas to keep in mind

- **Browser autoplay policy:** audio can only start from a user gesture — `await Tone.start()`
  inside the button `onClick` satisfies this. Don't call it on page load or it will be
  blocked.
- **Tone.js and SSR:** Docusaurus pre-renders pages at build time where `window`/`AudioContext`
  don't exist. Keep all Tone calls inside event handlers/effects (the component above does),
  and embed the player via `<BrowserOnly>` so the build doesn't try to render audio code on
  the server.
- **Unicode vs LaTeX:** the README uses `−` (U+2212), `·`, `≥`, superscripts. Normalize
  these when converting or KaTeX will complain.
- **baseUrl:** the single most common GitHub Pages mistake — broken CSS/links means
  `baseUrl` doesn't match the repo path.
- **Monorepo hygiene:** keep all Node/`npm` commands scoped to `website/`; the .NET build
  and the site build stay independent except for the optional Phase 5 export step.
