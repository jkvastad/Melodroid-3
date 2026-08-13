import React, {useEffect, useMemo, useRef, useState} from 'react';
import * as Tone from 'tone';
import useBaseUrl from '@docusaurus/useBaseUrl';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import {
  generatePattern,
  gridLines,
  groupPulseRanges,
  mulberry32,
  parseMeter,
  parsePhrase,
  parseSubdivisions,
  type GroupRepeat,
  type Pulse,
} from '@site/src/lib/rhythmPattern';
import {
  findSupersets,
  placementKeys,
  type Superset,
} from '@site/src/lib/placements';
import {
  bakeMelody,
  expandPlacements,
  generateChordWalk,
  generateOpenWalk,
  parsePhraseBinds,
  placementPatternLabel,
  type CuratedPlacement,
  type PhraseBindAxis,
  type PlacementPattern,
  type WalkBinding,
} from '@site/src/lib/chordWalk';
import {
  generatePerceptionLoop,
  generatePerceptionWalk,
  type PerceptionStep,
} from '@site/src/lib/perceptionTables';
import {enumerateAll, type Voicing} from '@site/src/lib/voicings';
import {PIANO_URLS, PIANO_SAMPLE_PATH} from '@site/src/lib/piano';

export type RhythmPatternPlayerProps = {
  meter?: string; // initial meter, e.g. '4' or '7 2 3'; default '4'
  subdivisions?: string; // initial subdivision spec; default '2'
  bpm?: number; // initial unit-beat tempo; default 100
  minBpm?: number; // tempo slider bounds; default 40 / 240
  maxBpm?: number;
  syncopation?: number; // initial [0,1]; default 0
  resolution?: number; // initial [0,1]; default 1
  phrases?: boolean; // show the phrase-repetition control (opt-in); default false
  phrase?: string; // initial phrase scheme, e.g. 'ABAC' or 'A Ba A Ca'; default ''
  pitchHz?: number; // fixed blip pitch in Hz; default 165
  height?: number; // plot height in px; default 240
  melody?: boolean; // show the lcm-family melody controls (this page only); default false
  chord?: boolean; // chord mode: roll a random chord, find the LCM families whose placement
  // contains it, draw melody from a matched family, and sound the chord as long notes
  // re-struck each meter group (this page only); default false
  presets?: DuetChord[]; // guided chord mode (with `chord`): fixed chord + melody-set choices
  // via two linked dropdowns instead of a random roll (this page only)
  progression?: ProgressionSet[]; // progression mode: loop random minor-second-free triads
  // drawn from a fixed set (one per meter group), melody drawn from the whole set (this page only)
  chordWalk?: ChordWalkSet; // chord-walk mode: a *cyclic* progression — one chord per meter group,
  // adjacent (and wrap) chords linked by a shared curated placement, melody drawn from the bridging
  // placement, returning to the origin chord (this page only)
  phraseBinds?: string; // in chord-walk mode, which axes the phrase binds: a '+'-joined subset
  // of 'chords' · 'placements' · 'melody' (e.g. 'chords+placements+melody'), or 'rhythm' / '' for
  // none (default, rhythm only). Sets the initial checkbox state; only meaningful with both
  // `phrases` and `chordWalk` (this page only)
  perceptionWalk?: PerceptionWalkSet; // perception-walk mode: an OPEN progression where each meter
  // group picks a random opening key from the current chord's perception table, commits to that
  // entry's placement (melody drawn from it, opening key sounded first), and wanders to the next
  // chord via the current chord's stable melodic supersets. Vocabulary is major/minor/dim. Takes
  // precedence over `chordWalk` if both are set (this page only)
};

// A guided-mode melody source: either an `lcm@at` placement (resolved to keys via
// placementKeys) or an explicit folded key set (for scales that are not a plain placement,
// e.g. the harmonic minor `0 3 4 6 7 9 11`). `label` is what the dropdown shows.
export type DuetMelody = {label: string; lcm?: number; at?: number; keys?: number[]};
// A guided-mode chord: its 12-tet keys (sounded as the low, re-struck accompaniment) plus
// the melody sets offered under it. Two of these (minor / major) drive the two dropdowns.
export type DuetChord = {label: string; keys: number[]; melodies: DuetMelody[]};

// A progression-mode source set: a labelled key set (12-tet pitch classes) the progression
// engine draws minor-second-free triads from, and from which the melody is drawn. The set
// dropdown lists these; the first is the default (e.g. the stable 15s@0 vs collapsed 24@1/8).
// Optional `chord` pins the per-group chord to that single voicing (heuristic-independent,
// same chord every group) while the melody still draws from the full `keys` — for auditioning
// a scale's characteristic chord against the random draws.
export type ProgressionSet = {label: string; keys: number[]; chord?: number[]};

// Chord-walk-mode authored config: the ORIGIN chord (its 12-tet pitch classes) and the curated
// placement PATTERNS (each an lcm family like `{lcm: 8}` or an explicit key set like the stable-15
// subset `{label: '15s', keys: [0,1,3,5,9,10]}`). expandPlacements rotates every pattern to all 12
// anchors to build the curated pool; generateChordWalk then walks tertian chords (chosen heuristic)
// that are subsets of ≥1 pool placement, in a cycle that returns to the origin.
export type ChordWalkSet = {origin: number[]; placements: PlacementPattern[]};

// Perception-walk-mode authored config: an optional START chord (its 12-tet pitch classes). When
// omitted the walk begins on a random major/minor/dim triad (seeded). The chord vocabulary and the
// perception tables are fixed (major/minor/dim), so there is nothing else to author.
export type PerceptionWalkSet = {start?: number[]};

// The baked per-group step shape the scheduler consumes, shared by chord-walk and perception-walk.
// `openingKey` is only populated by perception-walk (chord-walk leaves it absent).
type BakedWalkStep = {
  triad: number[];
  offsets: number[] | null;
  melodyKeys: number[] | null;
  placementLabel: string | null;
  openingKey?: number | null;
};

// The melody source per group. Only 'bridging' (draw from the placement containing both this chord
// and the next) is implemented; the constant documents the seam for a future 'any-current' strategy
// (draw from any placement containing the current chord).
type WalkMelodyStrategy = 'bridging';
const WALK_MELODY_STRATEGY: WalkMelodyStrategy = 'bridging';

// A raw chord-walk step before baking: the chord's pitch classes and the placement it bridges from
// (null only in the degenerate origin-repeat fallback). generateChordWalk returns the non-null form.
type RawWalkStep = {chord: number[]; bridgingPlacement: CuratedPlacement | null};

// Map a raw walk to the scheduler's baked step shape: the chord's pitch classes (`triad`), its
// voiced offsets, the folded melody pool (per WALK_MELODY_STRATEGY), and the placement label for the
// "playing" readout. Shared by the chordWalkSteps memo and the per-cycle regeneration in play() so
// both bake identically.
function bakeWalkSteps(walk: RawWalkStep[], pitchHz: number): BakedWalkStep[] {
  return walk.map((s) => ({
    triad: s.chord,
    offsets: chordOffsets(s.chord, pitchHz),
    melodyKeys:
      WALK_MELODY_STRATEGY === 'bridging' && s.bridgingPlacement
        ? foldOctave(s.bridgingPlacement.keys)
        : null,
    placementLabel: s.bridgingPlacement?.label ?? null,
  }));
}

// Map a perception walk to the same baked step shape, carrying the opening key (the unit's first
// note, sounded so the early key that chose the placement is heard). The melody pool is the chosen
// placement's folded key set.
function bakePerceptionSteps(steps: PerceptionStep[], pitchHz: number): BakedWalkStep[] {
  return steps.map((s) => ({
    triad: s.chord,
    offsets: chordOffsets(s.chord, pitchHz),
    melodyKeys: foldOctave(s.placement.keys),
    placementLabel: s.placement.label,
    openingKey: ((s.openingKey % 12) + 12) % 12,
  }));
}

// The degenerate walk used when generateChordWalk finds no length-N cycle: the origin chord repeated
// in every group, bridged by any curated placement containing it (or null → no melody that group).
function originWalk(origin: number[], curatedPool: CuratedPlacement[], N: number): RawWalkStep[] {
  return Array.from({length: N}, () => ({
    chord: origin,
    bridgingPlacement: curatedPool.find((p) => origin.every((k) => p.keys.includes(k))) ?? null,
  }));
}

// The three phrase-bind axes, shown as independent checkboxes in chord-walk mode. Binding chords
// and/or placements constrains the walk (see WalkBinding / deriveWalkBindings); binding melody
// restates a repeated group's melodic motif (see bakeMelody). None checked = rhythm only (the
// phrase repeats the rhythm alone, today's default).
const PHRASE_BIND_AXES: {id: PhraseBindAxis; label: string}[] = [
  {id: 'chords', label: 'chords'},
  {id: 'placements', label: 'placements'},
  {id: 'melody', label: 'melody'},
];

// Translate the rhythm phrase (one GroupRepeat per meter group) into per-group walk bindings for
// the chord/placement axes. Only FULL repeats (fullSource) bind harmony — a half-repeat has no
// "first half" of a chord — and only when that axis is enabled. Returns null when nothing is bound
// (neither axis, no phrase, or no full repeats), so callers skip the bound generation entirely.
// (Melody binding is handled separately in bakeMelody — it needs no walk constraint.)
function deriveWalkBindings(
  repeats: GroupRepeat[] | null,
  bindChords: boolean,
  bindPlacements: boolean,
): WalkBinding[] | null {
  if ((!bindChords && !bindPlacements) || !repeats) return null;
  let any = false;
  const bindings = repeats.map((rep) => {
    const src = rep.fullSource; // half-repeats never bind harmony
    const chordSource = bindChords ? src : null;
    const placementSource = bindPlacements ? src : null;
    if (chordSource !== null || placementSource !== null) any = true;
    return {chordSource, placementSource};
  });
  return any ? bindings : null;
}

// Generate a chord walk honoring phrase bindings, relaxing to a free (unbound) walk only after the
// bound search has EXHAUSTIVELY proven no cycle exists (generateChordWalk backtracks fully, so a
// null under active bindings is that proof). Returns the walk plus `unsatisfied` — true iff a
// binding was requested but could not close a cycle. Never returns null: falls back to originWalk
// when even the free walk fails.
function generateWalkRelaxed(
  origin: number[],
  candidates: number[][],
  pool: CuratedPlacement[],
  N: number,
  seed: number,
  bindings: WalkBinding[] | null,
): {walk: RawWalkStep[]; unsatisfied: boolean} {
  if (bindings) {
    const bound = generateChordWalk(origin, candidates, pool, N, seed, bindings);
    if (bound) return {walk: bound, unsatisfied: false};
  }
  const free = generateChordWalk(origin, candidates, pool, N, seed) ?? originWalk(origin, pool, N);
  return {walk: free, unsatisfied: bindings != null};
}

// Free-roam analogue of generateWalkRelaxed: generate an OPEN walk from `start` (no return-to-origin
// closure), honoring phrase bindings and relaxing to an unbound open walk only after the bound search
// exhausts. Also returns `next` — the carry chord the following loop pass starts from. Never returns
// null: falls back to `start` repeated (carry = start) when even the free open walk fails.
function generateOpenWalkRelaxed(
  start: number[],
  candidates: number[][],
  pool: CuratedPlacement[],
  N: number,
  seed: number,
  bindings: WalkBinding[] | null,
): {walk: RawWalkStep[]; next: number[]; unsatisfied: boolean} {
  if (bindings) {
    const bound = generateOpenWalk(start, candidates, pool, N, seed, bindings);
    if (bound) return {walk: bound.steps, next: bound.next, unsatisfied: false};
  }
  const free = generateOpenWalk(start, candidates, pool, N, seed);
  if (free) return {walk: free.steps, next: free.next, unsatisfied: bindings != null};
  return {walk: originWalk(start, pool, N), next: start, unsatisfied: bindings != null};
}

// Progression heuristics — how the per-group chords are chosen from the set. Each maps to a
// chord-pool generator (see progressionTriads memo): "tertian-triads" draws only the four
// 3-note triad qualities (maj/min/dim/aug); "tertian-chords" draws any stack of thirds (those
// triads plus the tertian seventh chords); "random-triads" draws any minor-second-free 3-note
// triad. The set constrains which of these actually appear.
type ProgressionHeuristic = {
  id: 'tertian-triads' | 'major-minor-triads' | 'tertian-chords' | 'random-triads';
  label: string;
};
const PROGRESSION_HEURISTICS: ProgressionHeuristic[] = [
  {id: 'tertian-triads', label: 'Triads (maj/min/dim/aug)'},
  {id: 'major-minor-triads', label: 'Major and minor triads'},
  {id: 'tertian-chords', label: 'Tertian (triads + 7ths)'},
  {id: 'random-triads', label: 'Random triads (no m2)'},
];

// Chord mode only auditions LCM families within the study range; larger folded LCMs
// (a chord can sit inside placements of much larger families) are not offered as
// interpretations. Passed to findSupersets as its maxLcm.
const MAX_CHORD_LCM = 24;

// Chord-mode accompaniment floor: the voicing is dropped an octave below the melody, so its
// bottom note (its root) can land very low. Skip to the next-best voicing whose bottom note
// clears this (~C3) rather than let the chord get muddy.
const MIN_CHORD_HZ = 130;

// Roll a random chord (2–7 distinct chromatic keys) and find its superset placements, retrying
// until it is a subset of at least one LCM family placement with LCM ≤ MAX_CHORD_LCM. Returns
// the chord together with those matches (ranked tightest-first). A large but bounded retry cap
// guards against the rare no-match draw; the [0,4,7] major triad is a guaranteed-legal fallback
// if the cap is ever hit.
function rollChord(rng: () => number): {keys: number[]; matches: Superset[]} {
  for (let attempt = 0; attempt < 500; attempt++) {
    const size = 2 + Math.floor(rng() * 6); // 2..7
    const pool = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
    // Partial Fisher–Yates: take the first `size` after shuffling those slots.
    for (let i = 0; i < size; i++) {
      const j = i + Math.floor(rng() * (pool.length - i));
      [pool[i], pool[j]] = [pool[j], pool[i]];
    }
    const keys = pool.slice(0, size).sort((a, b) => a - b);
    const matches = findSupersets(keys, 12, MAX_CHORD_LCM);
    if (matches.length > 0) return {keys, matches};
  }
  const keys = [0, 4, 7];
  return {keys, matches: findSupersets(keys, 12, MAX_CHORD_LCM)};
}

// Parse a user-typed chord ("0 4 7" / "0,4,7") into distinct pitch classes 0..11, sorted
// ascending (matching how rollChord sorts before sweeping). Throws Error with a friendly
// message on bad input so the client can show an inline hint and keep the last valid chord.
// The 2..7 count mirrors rollChord's range — no LCM family exceeds 7 members.
function parseChordKeys(text: string): number[] {
  const tokens = text.split(/[\s,]+/).filter((t) => t.length > 0);
  const keys = tokens.map((t) => {
    if (!/^\d+$/.test(t)) throw new Error(`"${t}" is not a whole number.`);
    const n = parseInt(t, 10);
    if (n < 0 || n > 11) throw new Error(`Keys must be 0–11, got ${n}.`);
    return n;
  });
  if (new Set(keys).size !== keys.length)
    throw new Error('Chord keys must be distinct.');
  if (keys.length < 2 || keys.length > 7)
    throw new Error('Enter 2–7 keys, e.g. "0 4 7".');
  return [...keys].sort((a, b) => a - b);
}

// Voice a chord as semitone offsets from pitchHz (key 0 = pitchHz, the melody's root):
// an ascending, semitone-avoiding voicing with its root dropped one octave below the melody
// octave (the `- 12`), keeping each note's pitch class aligned with the melody. Normally the
// lowest-penalty voicing, but the octave drop can push its bottom note (always its root) very
// low, so we skip to the next-best voicing whose bottom note clears MIN_CHORD_HZ. When no
// voicing clears it (all chord keys are low) we take the highest-rooted one (least low). Falls
// back to the raw chord placed an octave below when no semitone-free voicing exists at all
// (the rare all-semitone chord, e.g. a bare semitone dyad).
function chordOffsets(keys: number[] | null, pitchHz: number): number[] | null {
  if (!keys || keys.length === 0) return null;
  const voicings = enumerateAll(keys).sort(
    (a, b) => a.penalty - b.penalty || a.span - b.span,
  );
  if (voicings.length === 0) return keys.map((k) => k - 12); // all-semitone chord
  // Bottom note is the root, dropped one octave; keep it at/above the threshold.
  const clears = (v: Voicing) =>
    pitchHz * Math.pow(2, (v.root - 12) / 12) >= MIN_CHORD_HZ;
  const pick =
    voicings.find(clears) ??
    voicings.reduce((a, b) => (b.root > a.root ? b : a)); // none clears: least low
  return pick.offsets.map((off) => pick.root - 12 + off);
}

// A match labelled for the dropdown, e.g. "24 @ 0" (LCM family 24 anchored at key 0).
const matchLabel = (m: Superset): string => `${m.lcm} @ ${m.at}`;

// The LCM families of the intro table on voicings-and-lcm-families.mdx, keyed to that
// table's rows. `keys` holds the raw table voicing (so the provenance is visible); the
// player folds them into a single octave before drawing pitches from them. The leading
// id '0' is not a family: it is the chromatic draw pool — all 12 pitch classes, a uniform
// random draw over the whole octave rather than a good-fraction subset. The RANDOM_ID
// entry is not a family either: it draws a continuous frequency anywhere in the octave
// (a real key in [0,12), unquantized) to contrast truly random pitch against the 12 keys;
// its `keys` is unused (the octaveKeys memo short-circuits on RANDOM_ID).
const RANDOM_ID = 'random';
type LcmFamily = {id: string; label: string; keys: number[]};
const LCM_FAMILIES: LcmFamily[] = [
  {id: '0', label: '0 · Chromatic', keys: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]},
  {id: RANDOM_ID, label: '∅ · Random Pitch', keys: []},
  {id: 'major-pentatonic', label: 'Major Pentatonic', keys: [0, 2, 4, 7, 9]},
  {id: 'harmonic-minor', label: 'Harmonic Minor', keys: [0, 1, 4, 5, 7, 8, 10]},
  {id: 'whole-tone', label: 'Whole Tone', keys: [0, 2, 4, 6, 8, 10]},
  {id: 'diminished-7', label: 'Diminished 7th', keys: [0, 3, 6, 9]},
  {id: 'augmented', label: 'Augmented', keys: [0, 4, 8]},
  {id: '1', label: '1 · Unison', keys: [0]},
  {id: '2', label: '2 · Perfect Fifth', keys: [0, 7]},
  {id: '3,4', label: '3,4 · Major Third', keys: [0, 4, 7]},
  {id: '5,6', label: '5,6 · Add 9', keys: [0, 2, 4, 7]},
  {id: '8,9,10,12', label: '8,9,10,12 · Major 9', keys: [0, 4, 7, 11, 14]},
  {id: '15', label: '15', keys: [0, 3, 7, 11, 14, 17, 22]},
  {id: '18', label: '18 · Minor 11', keys: [0, 3, 7, 10, 14, 17]},
  {id: '20', label: '20', keys: [0, 4, 8, 11, 15, 18]},
  {id: '24', label: '24 · Major 13', keys: [0, 4, 7, 10, 14, 17, 21]},
];

// Fold a voicing into one octave of distinct pitch classes [0,12), sorted low → high —
// "an octave of pitches comprising the lcm family" for the melody to draw from.
const foldOctave = (keys: number[]): number[] =>
  [...new Set(keys.map((k) => ((k % 12) + 12) % 12))].sort((a, b) => a - b);

// Progression-mode chord pool: every 3-subset of a folded set that is free of minor seconds
// (no pair a circular semitone apart, i.e. min(d, 12 - d) !== 1). Returns the raw triads as
// pitch-class arrays for chordOffsets to voice. Empty only for pathological sets (< 3 keys or
// all-adjacent), which the progression bake guards against.
function m2FreeTriads(set: number[]): number[][] {
  const s = foldOctave(set);
  const triads: number[][] = [];
  for (let a = 0; a < s.length; a++)
    for (let b = a + 1; b < s.length; b++)
      for (let c = b + 1; c < s.length; c++) {
        const t = [s[a], s[b], s[c]];
        const m2 = [
          [t[0], t[1]],
          [t[0], t[2]],
          [t[1], t[2]],
        ].some(([x, y]) => {
          const d = Math.abs(x - y);
          return Math.min(d, 12 - d) === 1;
        });
        if (!m2) triads.push(t);
      }
  return triads;
}

// All size-k subsets of arr, as index-ordered arrays. Used only on folded key sets (≤ 12
// elements, in practice ≤ 7), so the naive recursion is fine.
function combinations<T>(arr: T[], k: number): T[][] {
  if (k === 0) return [[]];
  if (k > arr.length) return [];
  const out: T[][] = [];
  for (let i = 0; i <= arr.length - k; i++)
    for (const rest of combinations(arr.slice(i + 1), k - 1))
      out.push([arr[i], ...rest]);
  return out;
}

// True iff the pitch classes form a stack of thirds — some note is a root from which the
// others rise by adjacent intervals all in {3, 4}. Covers the four triad qualities (2 thirds)
// and every tertian seventh chord (3 thirds: maj7, dom7, min7, min-maj7, half-dim7, dim7, aug).
function isTertian(notes: number[]): boolean {
  for (const root of notes) {
    const offs = notes.map((n) => ((n - root) % 12 + 12) % 12).sort((a, b) => a - b);
    let ok = true;
    for (let i = 1; i < offs.length; i++) {
      const d = offs[i] - offs[i - 1];
      if (d !== 3 && d !== 4) {
        ok = false;
        break;
      }
    }
    if (ok) return true;
  }
  return false;
}

// Progression-mode chord pool: every 3- and 4-note subset of a folded set that is a stack of
// thirds (isTertian). Mixes triads and seventh chords into one pool for chordOffsets to voice.
// Tertian chords are inherently minor-second-free (min interval 3). Empty for sparse sets that
// admit no third-stack, which the progression bake / live draw guard against.
function tertianChords(set: number[]): number[][] {
  const s = foldOctave(set);
  return [...combinations(s, 3), ...combinations(s, 4)].filter(isTertian);
}

// Progression-mode chord pool: only the four 3-note tertian triad qualities — major
// [0,4,7], minor [0,3,7], diminished [0,3,6], augmented [0,4,8]. Same third-stack filter
// as tertianChords but 3-note subsets only, so no seventh chords appear.
function tertianTriads(set: number[]): number[][] {
  return combinations(foldOctave(set), 3).filter(isTertian);
}

// Major [0,4,7] or minor [0,3,7] triad, checked from every possible root so all rootings of
// the folded pitch-class set qualify. Excludes diminished/augmented (which isTertian accepts).
function isMajorOrMinorTriad(notes: number[]): boolean {
  for (const root of notes) {
    const offs = notes.map((n) => ((n - root) % 12 + 12) % 12).sort((a, b) => a - b);
    if ((offs[1] === 4 && offs[2] === 7) || (offs[1] === 3 && offs[2] === 7)) return true;
  }
  return false;
}

// Progression-mode chord pool: only major and minor 3-note triads — a stricter subset of
// tertianTriads that drops the diminished and augmented qualities.
function majorMinorTriads(set: number[]): number[][] {
  return combinations(foldOctave(set), 3).filter(isMajorOrMinorTriad);
}

// The pulses that actually sound, in the order the scheduler fires them. Factored so the
// baked melody assigns one key per event in exactly that order (play() reuses this).
const firingEvents = (pulses: Pulse[]): Pulse[] =>
  pulses.filter((p) => p.velocity > 0).sort((a, b) => a.unitBeat - b.unitBeat);

// Cumulative meter-group start beats, e.g. [4, 4, 4] → [0, 4, 8]. Used by chord-walk mode to
// bucket a firing event into its meter group (so each group draws melody from its own placement).
const groupStartBeats = (meter: number[]): number[] => {
  const starts: number[] = [];
  let acc = 0;
  for (const m of meter) {
    starts.push(acc);
    acc += m;
  }
  return starts;
};

// The meter-group index a firing event's unitBeat falls in, given the group starts (ascending).
const groupIndexOf = (unitBeat: number, starts: number[]): number => {
  let gi = 0;
  for (let g = 0; g < starts.length; g++) if (unitBeat >= starts[g]) gi = g;
  return gi;
};

// Index into `pulses` of each firing event, in firing order — the bridge from a per-event
// key list (bakedKeys / a live loop-off roll) back to the per-bar color array, which is
// indexed by position in the full `pulses` array. Reference identity is safe because
// firingEvents just filters the same objects out of `pulses`.
const firingPulseIndices = (pulses: Pulse[]): number[] => {
  const idxOf = new Map<Pulse, number>(pulses.map((p, i) => [p, i]));
  return firingEvents(pulses).map((e) => idxOf.get(e)!);
};

// Perception-walk: overwrite each meter group's FIRST firing event with that group's opening key,
// so the note that selected the placement/perception is actually sounded first. `keys` is one key
// per firing event (in firing order); `firingPulseIdx` are those events' pulse indices (same
// order); `ranges` the per-group pulse ranges; `steps` the baked walk steps (openingKey per group).
// Returns a new array. Used for both the baked phrase and the live scheduler draw so all paths open
// on the perception's key.
function applyOpeningKeys(
  keys: number[],
  firingPulseIdx: number[],
  ranges: {start: number; count: number}[],
  steps: BakedWalkStep[],
): number[] {
  const out = [...keys];
  const groupOf = (pi: number): number => {
    for (let g = 0; g < ranges.length; g++)
      if (pi >= ranges[g].start && pi < ranges[g].start + ranges[g].count) return g;
    return ranges.length - 1;
  };
  const seen = new Set<number>();
  firingPulseIdx.forEach((pi, e) => {
    const g = groupOf(pi);
    if (seen.has(g)) return;
    seen.add(g);
    const ok = steps[g]?.openingKey;
    if (ok != null) out[e] = ((ok % 12) + 12) % 12;
  });
  return out;
}

// Default (no-melody) bar colors — the original single blue used before pitch coloring.
const BLUE_FILL = 'rgba(30,90,168,0.55)';
const BLUE_STROKE = 'rgba(30,90,168,0.95)';

// Map a pitch class to a visible-spectrum colour: low pitch (long wavelength) → red,
// high pitch → blue/violet. Hue 0° (red) … 285° (violet) across the octave; the pitch
// class is folded into [0,12) first so any raw key lands somewhere on the gradient.
const spectrumHue = (key: number): number => ((((key % 12) + 12) % 12) / 12) * 285;
const pitchFill = (key: number): string => `hsla(${spectrumHue(key)}, 85%, 55%, 0.6)`;
const pitchStroke = (key: number): string => `hsl(${spectrumHue(key)}, 85%, 45%)`;

// The same red→violet ramp as the bars, as a CSS gradient for the plot's legend swatch:
// one stop per pitch class 0…11 so the legend gradient matches the bar colours exactly.
const SPECTRUM_GRADIENT = `linear-gradient(to right, ${Array.from(
  {length: 12},
  (_, k) => `hsl(${spectrumHue(k)}, 85%, 55%)`,
).join(', ')})`;

// A rendered pattern together with the meter/subdivisions it was built from, so the
// plot's grid lines and x-range always match the bars (both only change on Generate).
type RenderedPattern = {
  pulses: Pulse[];
  totalBeats: number;
  meter: number[];
  subdivisions: number[];
};

type GridSpec = ReturnType<typeof gridLines>;

// Background grid drawn behind the bars (drawClear fires before the series): faint at
// every pulse, medium at every unit beat, bold at the meter accents. Mirrors the
// vertical-line plugins in WavePlotClient / PartialSweepPlot.
function gridPlugin(lines: GridSpec): uPlot.Plugin {
  const stroke = (u: uPlot, xs: number[], color: string, width: number) => {
    const {ctx} = u;
    ctx.beginPath();
    ctx.lineWidth = width;
    ctx.strokeStyle = color;
    const top = u.bbox.top;
    const bot = u.bbox.top + u.bbox.height;
    for (const x of xs) {
      const cx = Math.round(u.valToPos(x, 'x', true));
      ctx.moveTo(cx, top);
      ctx.lineTo(cx, bot);
    }
    ctx.stroke();
  };
  return {
    hooks: {
      drawClear: (u) => {
        u.ctx.save();
        stroke(u, lines.pulses, 'rgba(120,120,140,0.18)', 1);
        stroke(u, lines.unitBeats, 'rgba(90,90,120,0.4)', 1);
        stroke(u, lines.groupStarts, 'rgba(40,40,70,0.7)', 2);
        u.ctx.restore();
      },
    },
  };
}

// "Sing-along" playhead: marks the latest-sounded bar during playback. Drawn on the `draw`
// hook (after the series, so it sits on top of the bars). `beatRef` holds the current bar's
// unitBeat x, or null when stopped. Reads the ref at draw time so a cheap redraw() moves it.
function playheadPlugin(beatRef: {current: number | null}): uPlot.Plugin {
  const FILL = '#f08c00'; // amber — reads over both the blue and spectrum bars, either theme
  const STROKE = 'rgba(80,40,0,0.9)';
  return {
    hooks: {
      draw: (u) => {
        const beat = beatRef.current;
        if (beat == null) return;
        const cx = Math.round(u.valToPos(beat, 'x', true));
        const top = u.bbox.top;
        const base = u.bbox.top + u.bbox.height; // baseline (velocity 0), foot of the bars
        const {ctx} = u;
        ctx.save();
        // Faint vertical guide through the current bar.
        ctx.beginPath();
        ctx.lineWidth = 1;
        ctx.strokeStyle = 'rgba(240,140,0,0.35)';
        ctx.moveTo(cx, top);
        ctx.lineTo(cx, base);
        ctx.stroke();
        // Upward-pointing triangle in the bottom margin, apex at the baseline.
        const half = 6;
        const h = 9;
        ctx.beginPath();
        ctx.moveTo(cx, base); // apex (points up at the bar)
        ctx.lineTo(cx - half, base + h);
        ctx.lineTo(cx + half, base + h);
        ctx.closePath();
        ctx.fillStyle = FILL;
        ctx.fill();
        ctx.lineWidth = 1;
        ctx.strokeStyle = STROKE;
        ctx.stroke();
        ctx.restore();
      },
    },
  };
}

// syncopation and resolution live in [0,1]; clamp typed entries into range.
const clampUnit = (x: number): number => Math.max(0, Math.min(1, x));

// Render a unit value with at least one decimal place so the number boxes read as
// fractional (0 → "0.0", 1 → "1.0") without truncating finer slider steps
// (0.37 stays "0.37").
function fmtUnit(x: number): string {
  const s = String(x);
  return s.includes('.') ? s : s + '.0';
}

export default function RhythmPatternPlayerClient({
  meter: meterProp = '4',
  subdivisions: subProp = '2',
  bpm: bpmProp = 100,
  minBpm = 40,
  maxBpm = 240,
  syncopation: syncProp = 0,
  resolution: resProp = 1,
  phrases = false,
  phrase: phraseProp = '',
  pitchHz = 196,
  height = 240,
  melody = false,
  chord = false,
  presets,
  progression,
  chordWalk,
  phraseBinds: phraseBindsProp = 'rhythm',
  perceptionWalk,
}: RhythmPatternPlayerProps) {
  // Guided chord mode: the chord and its melody sets come from `presets` (two linked
  // dropdowns) instead of a random roll. Still runs the full chord-mode engine, so it
  // requires `chord` — the only divergences are the chord/melody *sources* and the controls.
  const guided = chord && presets != null;
  // Progression mode: loop a baked progression of minor-second-free triads drawn from a fixed
  // set (one triad per meter group), with the melody drawn from the whole set. Runs the chord
  // engine like guided mode but chooses a fresh voicing per group instead of one static chord.
  const progressionOn = progression != null && progression.length > 0;
  // Chord-walk mode: a cyclic progression where each meter group sounds one chord, adjacent (and
  // wrap) chords share a curated placement, and the melody is drawn from the bridging placement.
  // Runs the chord engine like progression mode but the per-group chord/melody come from a baked
  // walk that returns to the origin instead of independent random triads.
  // Perception-walk mode: an OPEN progression driven by per-group perception tables (see
  // perceptionTables.ts). Runs the chord engine like chord-walk but the per-group chord/melody come
  // from a perception walk that always wanders (never closes to an origin). Takes precedence over
  // chord-walk when both props are set.
  const perceptionWalkOn = perceptionWalk != null;
  const chordWalkOn =
    !perceptionWalkOn && chordWalk != null && chordWalk.placements.length > 0;
  // Parse the author-supplied defaults once, falling back to a sane starter if the
  // MDX passes something malformed.
  const initial = useMemo(() => {
    let m: number[];
    try {
      m = parseMeter(meterProp);
    } catch {
      m = [4];
    }
    let s: number[];
    try {
      s = parseSubdivisions(subProp, m.length);
    } catch {
      s = Array<number>(m.length).fill(2);
    }
    // Parse the initial phrase against the resolved meter/subdivisions; a malformed default
    // just starts with no repetition rather than blocking the player.
    let r: GroupRepeat[] | null;
    try {
      r = phraseProp.trim().length === 0 ? null : parsePhrase(phraseProp, m, s);
    } catch {
      r = null;
    }
    return {m, s, r};
  }, [meterProp, subProp, phraseProp]);

  // Controls: text mirrors the input box; the parsed value is the last *valid* parse.
  const [meterText, setMeterText] = useState(meterProp);
  const [meter, setMeter] = useState<number[]>(initial.m);
  const [meterError, setMeterError] = useState<string | null>(null);
  const [subText, setSubText] = useState(subProp);
  const [subdivisions, setSubdivisions] = useState<number[]>(initial.s);
  const [subError, setSubError] = useState<string | null>(null);
  // Phrase repetition (only surfaced when `phrases`): the text mirrors the input box, the
  // parsed value is the last *valid* parse (null = no repetition), and phraseError holds an
  // inline validation message.
  const [phraseText, setPhraseText] = useState(phraseProp);
  const [phraseRepeats, setPhraseRepeats] = useState<GroupRepeat[] | null>(initial.r);
  const [phraseError, setPhraseError] = useState<string | null>(null);
  const [syncopation, setSyncopation] = useState(syncProp);
  const [syncText, setSyncText] = useState(() => fmtUnit(syncProp));
  const [resolution, setResolution] = useState(resProp);
  const [resText, setResText] = useState(() => fmtUnit(resProp));
  const [seed, setSeed] = useState(1);
  const [pattern, setPattern] = useState<RenderedPattern | null>(null);
  const [playing, setPlaying] = useState(false);

  // Instrument timbre: the theory-faithful sine/triangle synths (default) or a sampled
  // piano. Piano governs both the melody blips and (in chord mode) the chord accompaniment.
  // Read at Play time — changing it mid-playback applies on the next Play. `pianoLoading`
  // gates the first Play with Piano selected while the samples fetch.
  const [instrument, setInstrument] = useState<'sine' | 'piano'>('sine');
  const [pianoLoading, setPianoLoading] = useState(false);

  // Melody (only surfaced when `melody`): which intro-table LCM family to draw pitches
  // from ('' = fixed pitch, today's behavior), and whether the drawn pitches are baked
  // into a repeating phrase (loop on) or re-rolled on every hit (loop off).
  const [selectedLcm, setSelectedLcm] = useState(melody ? '8,9,10,12' : '');
  const [loopMelody, setLoopMelody] = useState(false);
  // Progression mode: whether the chord progression is the baked, repeating phrase (loop on)
  // or a fresh random triad rolled at every meter group each cycle (loop off) — the chord
  // analogue of loopMelody. currentChord is the triad sounding now (its pitch classes), shown
  // in the "playing" readout and updated by the scheduler at each group start.
  const [loopChords, setLoopChords] = useState(false);
  // Chord-walk mode: free-roam. When off (default) the walk is a closed cycle returning to the
  // origin each loop pass; when on, each pass continues from the chord the previous pass ended on,
  // so the harmony wanders indefinitely (no effect while loop melody is on, which freezes the walk).
  const [freeRoam, setFreeRoam] = useState(false);
  const [currentChord, setCurrentChord] = useState<number[] | null>(null);
  // Chord-walk mode: the bridging placement the current group draws its melody from (its label,
  // e.g. "8 @ 5" / "15s @ 7"), shown in the "placement" readout beside currentChord and updated
  // by the scheduler at each group start (null when stopped / outside chord-walk mode).
  const [currentPlacement, setCurrentPlacement] = useState<string | null>(null);

  // Chord mode (only when `chord`): a randomly rolled chord together with the LCM family
  // placements that contain it as a subset, and which of those matches drives the melody. The
  // chord itself is re-rolled only on Generate; switching selectedMatchIdx re-interprets the
  // same chord as a different family (the "ambiguous context" of the surrounding prose).
  const [chordState, setChordState] = useState<{
    keys: number[];
    matches: Superset[];
  } | null>(null);
  const [selectedMatchIdx, setSelectedMatchIdx] = useState(0);
  // The chord input box: text mirrors what's typed; chordError holds an inline validation
  // message (the last valid chordState is retained on a bad edit).
  const [chordText, setChordText] = useState('');
  const [chordError, setChordError] = useState<string | null>(null);

  // Guided chord mode (only when `presets`): which preset chord, and which of its melody
  // sets, the two linked dropdowns have selected. The melody index resets to 0 on a chord
  // switch (its option list swaps with the chord).
  const [selectedChordIdx, setSelectedChordIdx] = useState(0);
  const [selectedMelodyIdx, setSelectedMelodyIdx] = useState(0);

  // Progression mode (only when `progression`): which source set (drawn from + voiced) and
  // which heuristic the two dropdowns have selected. Both re-bake the progression on change.
  const [selectedProgIdx, setSelectedProgIdx] = useState(0);
  const [selectedHeuristicIdx, setSelectedHeuristicIdx] = useState(0);

  // Chord-walk mode (only when `chordWalk`): which chord-vocabulary heuristic the dropdown has
  // selected (reuses PROGRESSION_HEURISTICS). Changing it re-bakes the walk. The curated pool and
  // origin are fixed by the prop, so there is no set dropdown here.
  const [walkHeuristicIdx, setWalkHeuristicIdx] = useState(0);
  // Chord-walk + phrase: which axes the rhythm phrase binds (see PHRASE_BIND_AXES) — independent
  // toggles for chord, placement, and melody. Chord/placement re-derive the walk bindings and
  // re-bake; melody re-bakes the melody phrase. Initialised from the `phraseBinds` prop.
  const initialBinds = useMemo(() => parsePhraseBinds(phraseBindsProp), [phraseBindsProp]);
  const [bindChords, setBindChords] = useState(initialBinds.chords);
  const [bindPlacements, setBindPlacements] = useState(initialBinds.placements);
  const [bindMelody, setBindMelody] = useState(initialBinds.melody);
  // Set when a requested chord/placement binding can't close a cycle (the walk then relaxes to a
  // free progression) — surfaced as an inline note.
  const [bindingUnsatisfied, setBindingUnsatisfied] = useState(false);
  // Chord-walk: which curated placement patterns are enabled (one boolean per authored pattern,
  // all on by default). Toggling a box narrows/widens the pool the walk draws from — the curated
  // pool memo filters on this, so everything downstream (candidates, bake, live re-roll) follows.
  // `chordWalk` is fixed per mounted instance, so a lazy initializer suffices.
  const [enabledPlacements, setEnabledPlacements] = useState<boolean[]>(
    () => (chordWalk?.placements ?? []).map(() => true),
  );
  // Toggle one placement, enforcing keep-at-least-one so the pool is never empty (an all-off pool
  // would degrade the walk to the origin repeated with no melody).
  const togglePlacement = (i: number) =>
    setEnabledPlacements((prev) => {
      const next = [...prev];
      next[i] = !next[i];
      if (next.every((b) => !b)) return prev; // never empty the pool
      return next;
    });

  // Live tempo: bpm drives the UI, tempoRef (seconds per unit beat) is read by the
  // scheduler each poll so a slider/number change retunes a running loop immediately.
  const [bpm, setBpm] = useState(bpmProp);
  const tempoRef = useRef(60 / bpmProp);
  const setBpmBoth = (b: number) => {
    const clamped = Math.max(minBpm, Math.min(maxBpm, Math.round(b)));
    tempoRef.current = 60 / clamped;
    setBpm(clamped);
  };

  const synthRef = useRef<Tone.Synth | null>(null);
  const gainRef = useRef<Tone.Gain | null>(null);
  // Chord-mode accompaniment: a polyphonic synth sounding the chord as long notes,
  // re-struck at each meter group start, beneath the melody blips.
  const chordSynthRef = useRef<Tone.PolySynth | null>(null);
  const chordGainRef = useRef<Tone.Gain | null>(null);
  // Sampled-piano voices for the "Piano" instrument (one for melody, one for the chord pad),
  // each on its own persistent gain node. Unlike the sine synths (rebuilt each Play), these
  // are built once and cached for the component's life so Sine↔Piano and Stop↔Play never
  // re-download or re-decode; disposed only on unmount. Sample paths honour the site baseUrl.
  const pianoMelodyRef = useRef<Tone.Sampler | null>(null);
  const pianoMelodyGainRef = useRef<Tone.Gain | null>(null);
  const pianoChordRef = useRef<Tone.Sampler | null>(null);
  const pianoChordGainRef = useRef<Tone.Gain | null>(null);
  const sampleBase = useBaseUrl(PIANO_SAMPLE_PATH);
  const loopTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const plotRef = useRef<uPlot | null>(null);

  // Per-bar spectrum colours, indexed by position in the current pattern's `pulses`
  // (null ⇒ fall back to flat blue). The uPlot bar series reads these via `disp`; the
  // baked-colour effect and the live loop-off scheduler write them and cheap-redraw.
  const fillColorsRef = useRef<string[] | null>(null);
  const strokeColorsRef = useRef<string[] | null>(null);

  // Sing-along playhead: the current bar's unitBeat (null when stopped), read by
  // playheadPlugin. playRunRef tags each play() run so scheduled callbacks left in flight
  // after Stop/Generate no-op — the global Tone.getDraw() is shared by both players on the
  // page, so we invalidate per-instance by run id rather than cancelling it globally.
  const playheadBeatRef = useRef<number | null>(null);
  const playRunRef = useRef(0);

  // --- Melody: pitch pool + per-event pitch assignment ---

  // Random Pitch draws a continuous key in [0,12) rather than a discrete family pool
  // (never in chord mode, which always draws from a chord-matched family).
  const isRandomPitch = !chord && selectedLcm === RANDOM_ID;

  // --- Chord-walk mode: curated pool → candidate chords → baked cyclic walk ---
  // (declared before octaveKeys because that memo reads the walk's first-group melody pool.)

  // The curated placement pool: every *enabled* authored pattern expanded to all 12 anchors. The
  // per-pattern checkboxes (enabledPlacements) filter which patterns contribute. Null outside
  // chord-walk mode.
  const curatedPool = useMemo(
    () =>
      chordWalkOn
        ? expandPlacements(chordWalk!.placements.filter((_, i) => enabledPlacements[i]))
        : null,
    [chordWalkOn, chordWalk, enabledPlacements],
  );
  // The chord vocabulary the walk may visit: heuristic chords (maj/min/dim/aug triads, or +7ths,
  // or any m2-free triad) that are a subset of ≥1 curated placement, deduped across placements.
  const walkCandidates = useMemo(() => {
    if (!chordWalkOn || !curatedPool) return null;
    const id = PROGRESSION_HEURISTICS[walkHeuristicIdx].id;
    const gen =
      id === 'tertian-triads'
        ? tertianTriads
        : id === 'major-minor-triads'
          ? majorMinorTriads
          : id === 'tertian-chords'
            ? tertianChords
            : m2FreeTriads;
    const seen = new Set<string>();
    const out: number[][] = [];
    for (const p of curatedPool)
      for (const c of gen(p.keys)) {
        const key = c.join(',');
        if (!seen.has(key)) {
          seen.add(key);
          out.push(c);
        }
      }
    return out;
  }, [chordWalkOn, curatedPool, walkHeuristicIdx]);
  // Per-group walk bindings derived from the phrase + chosen bind mode — null when nothing binds
  // ('rhythm' mode, no phrase, or no full repeats), so the walk stays free (today's behavior).
  const walkBindings = useMemo(
    () =>
      chordWalkOn
        ? deriveWalkBindings(phraseRepeats, bindChords, bindPlacements)
        : null,
    [chordWalkOn, phraseRepeats, bindChords, bindPlacements],
  );
  // The baked cyclic walk — one step per meter group: the chord to sound (its pitch classes + voiced
  // offsets) and the folded melody pool (the bridging placement, per WALK_MELODY_STRATEGY). Seeded
  // off the rhythm seed (salted, independent of the melody/progression bakes) so Generate re-rolls
  // deterministically. This is the FIRST cycle; with loop melody off the scheduler re-rolls a fresh
  // legal cycle each loop, and with loop melody on it freezes and repeats this one (its baked phrase
  // stays coherent). Honors the phrase bindings, relaxing to a free walk (and setting `unsatisfied`)
  // only after the bound search exhaustively proves no bound cycle exists; falls back to the origin
  // repeated in every group when even the free walk finds no cycle. Null outside chord-walk.
  const chordWalkRaw = useMemo(() => {
    if (!chordWalkOn || !pattern || !curatedPool || !walkCandidates) return null;
    const N = pattern.meter.length;
    const origin = foldOctave(chordWalk!.origin);
    const {walk, unsatisfied} = generateWalkRelaxed(
      origin,
      walkCandidates,
      curatedPool,
      N,
      seed ^ 0x2545f491,
      walkBindings,
    );
    return {steps: bakeWalkSteps(walk, pitchHz), unsatisfied};
  }, [chordWalkOn, pattern, curatedPool, walkCandidates, chordWalk, seed, pitchHz, walkBindings]);
  const chordWalkSteps = chordWalkRaw?.steps ?? null;
  // Reflect the first cycle's binding status into state for the inline note (setState in an effect,
  // not during render). The scheduler updates it again per re-rolled cycle (loop melody off).
  useEffect(() => {
    setBindingUnsatisfied(chordWalkRaw?.unsatisfied ?? false);
  }, [chordWalkRaw]);

  // The baked FIRST perception walk — one step per meter group (chord voicing + opening key +
  // perception + placement melody pool) plus `origin`, the resolved chord the loop closes back to
  // (a random maj/min triad when no `start` is given). A CLOSED loop: the last group is chosen so
  // its stable supersets contain the origin. Seeded off the rhythm seed so Generate re-rolls
  // deterministically; the scheduler re-loops around the same origin (or, in free roam, wanders
  // from the carry chord) unless loop melody is on (freezing this cycle). Null outside
  // perception-walk. `perceptionWalkSteps` is the unified baked-step form the octaveKeys /
  // bakedKeys / scheduler paths share with chord-walk.
  const perceptionWalkRaw = useMemo(() => {
    if (!perceptionWalkOn || !pattern) return null;
    const N = pattern.meter.length;
    const {steps, origin} = generatePerceptionLoop(
      perceptionWalk!.start ?? null,
      N,
      seed ^ 0x2545f491,
    );
    return {steps: bakePerceptionSteps(steps, pitchHz), origin};
  }, [perceptionWalkOn, pattern, perceptionWalk, seed, pitchHz]);
  const perceptionWalkSteps = perceptionWalkRaw?.steps ?? null;
  // The active walk's baked steps, whichever mode is on (mutually exclusive). Downstream melody /
  // scheduler code reads this rather than either mode's memo directly.
  const walkSteps: BakedWalkStep[] | null = chordWalkSteps ?? perceptionWalkSteps;

  // The pitch pool folded to one octave, or null for fixed pitch / random pitch. In chord
  // mode it is the selected match's LCM family placement (a superset of the chord); in
  // melody mode it is the chosen intro-table family.
  const octaveKeys = useMemo(() => {
    if (chordWalkOn || perceptionWalkOn) {
      // Walk modes: the melody pool changes per group; expose the first group's pool as the
      // representative so melodyOn / legend / spectrum colouring turn on. The scheduler and
      // bakedKeys pick the correct per-group pool.
      return walkSteps?.[0]?.melodyKeys ?? null;
    }
    if (progressionOn) {
      // Progression mode: the melody draws from the whole selected source set.
      return foldOctave(progression![selectedProgIdx].keys);
    }
    if (guided) {
      // Guided mode: the selected preset melody set — an lcm@at placement or explicit keys.
      const m = presets![selectedChordIdx]?.melodies[selectedMelodyIdx];
      if (!m) return null;
      return foldOctave(m.keys ?? placementKeys(m.lcm!, m.at!, 12));
    }
    if (chord) {
      const m = chordState?.matches[selectedMatchIdx];
      return m ? foldOctave(m.keys) : null;
    }
    if (selectedLcm === RANDOM_ID) return null;
    const fam = LCM_FAMILIES.find((f) => f.id === selectedLcm);
    return fam ? foldOctave(fam.keys) : null;
  }, [
    chordWalkOn,
    perceptionWalkOn,
    walkSteps,
    progressionOn,
    progression,
    selectedProgIdx,
    guided,
    presets,
    selectedChordIdx,
    selectedMelodyIdx,
    chord,
    chordState,
    selectedMatchIdx,
    selectedLcm,
  ]);
  // Melody is active (pitch varies + bars are spectrum-coloured) for a family or random
  // pitch — the single flag that replaces the old `octaveKeys`-truthiness tests.
  const melodyOn = isRandomPitch || octaveKeys != null;

  // Loop-on melody: one random key per firing event, drawn with the same seeded RNG as
  // the rhythm so a given (pattern, family, seed) always yields the same phrase. Re-rolls
  // when the rhythm (pattern/seed) or the family changes; null when fixed pitch.
  const bakedKeys = useMemo(() => {
    if (!pattern || !melodyOn) return null;
    // Chord-walk mode: each firing event draws from its meter group's bridging pool, so the baked
    // phrase modulates group-to-group. With melody binding on, a repeated group instead restates
    // the group it repeats's motif note-for-note (see bakeMelody); falls back to the representative
    // octaveKeys for any group whose bridging pool is empty (the origin-with-no-melody fallback).
    if ((chordWalkOn || perceptionWalkOn) && walkSteps) {
      const rng = mulberry32(seed ^ 0x51ed270b); // salt distinct from the walk seed
      const firingPulseIdx = firingPulseIndices(pattern.pulses);
      const ranges = groupPulseRanges(pattern.meter, pattern.subdivisions);
      const baked = bakeMelody(
        firingPulseIdx,
        ranges,
        walkSteps.map((s) => s.melodyKeys),
        octaveKeys,
        phraseRepeats,
        bindMelody,
        rng,
      );
      // Perception-walk: force each group's first note to its opening key (the note that chose the
      // placement); chord-walk has no opening key so its baked phrase is used as-is.
      return perceptionWalkOn
        ? applyOpeningKeys(baked, firingPulseIdx, ranges, walkSteps)
        : baked;
    }
    const rng = mulberry32(seed);
    return firingEvents(pattern.pulses).map(() =>
      isRandomPitch ? rng() * 12 : octaveKeys![Math.floor(rng() * octaveKeys!.length)],
    );
  }, [
    pattern,
    melodyOn,
    isRandomPitch,
    octaveKeys,
    seed,
    chordWalkOn,
    perceptionWalkOn,
    walkSteps,
    phraseRepeats,
    bindMelody,
  ]);

  // Mirror the melody config into refs so the look-ahead scheduler (play's pump) reads the
  // current values live, exactly like tempoRef — switching family / loop retunes a running
  // loop without a replay.
  const octaveKeysRef = useRef(octaveKeys);
  const bakedKeysRef = useRef(bakedKeys);
  const loopMelodyRef = useRef(loopMelody);
  const loopChordsRef = useRef(loopChords);
  const freeRoamRef = useRef(freeRoam);
  const melodyOnRef = useRef(melodyOn);
  const isRandomPitchRef = useRef(isRandomPitch);
  // The chord's voicing as semitone offsets from pitchHz, read by the scheduler so the
  // chord follows a newly generated one without a replay (mirrors octaveKeysRef): an
  // ascending, semitone-avoiding ordering (see §Scoring voicings) with its root dropped one
  // octave below the melody octave, so the chord underpins the melody instead of clustering on
  // top of it — the lowest-penalty such voicing whose bottom note clears MIN_CHORD_HZ (see
  // chordOffsets). Precomputed on a chord change so the scheduler need not re-voice per hit.
  const chordVoicingRef = useRef<number[] | null>(
    chordOffsets(chordState?.keys ?? null, pitchHz),
  );
  useEffect(() => {
    chordVoicingRef.current = chordOffsets(chordState?.keys ?? null, pitchHz);
  }, [chordState, pitchHz]);
  // The chord pool the selected source set can voice under the selected heuristic — the live
  // pool the scheduler draws from when "loop chords" is off (a fresh chord per group each
  // cycle). Recomputed on a set / heuristic switch. Null outside progression mode.
  const progressionTriads = useMemo(() => {
    if (!progressionOn) return null;
    const setEntry = progression![selectedProgIdx];
    // A pinned set collapses the pool to its one chord — every group plays it, heuristic ignored.
    if (setEntry.chord && setEntry.chord.length > 0) return [foldOctave(setEntry.chord)];
    const set = setEntry.keys;
    const id = PROGRESSION_HEURISTICS[selectedHeuristicIdx].id;
    switch (id) {
      case 'tertian-triads':
        return tertianTriads(set);
      case 'major-minor-triads':
        return majorMinorTriads(set);
      case 'tertian-chords':
        return tertianChords(set);
      case 'random-triads':
        return m2FreeTriads(set);
    }
  }, [progressionOn, progression, selectedProgIdx, selectedHeuristicIdx],
  );
  const progressionTriadsRef = useRef(progressionTriads);
  useEffect(() => {
    progressionTriadsRef.current = progressionTriads;
  }, [progressionTriads]);
  // Progression mode: the baked progression — one triad per meter group (its pitch classes, for
  // the "playing" readout, plus its voiced offsets), chosen from progressionTriads with the same
  // seeded RNG as the rhythm (salted so the chord draw is independent of the melody bake). This
  // is the phrase looped when "loop chords" is on; it re-bakes on Generate (seed) and on a set /
  // heuristic switch, so a running loop replays it until then. Null outside progression mode.
  const progressionChords = useMemo(() => {
    if (!progressionOn || !pattern || !progressionTriads || progressionTriads.length === 0)
      return null;
    const rng = mulberry32(seed ^ 0x9e3779b9);
    return pattern.meter.map(() => {
      const triad = progressionTriads[Math.floor(rng() * progressionTriads.length)];
      return {triad, offsets: chordOffsets(triad, pitchHz)};
    });
  }, [progressionOn, pattern, progressionTriads, seed, pitchHz]);
  const progressionChordsRef = useRef(progressionChords);
  useEffect(() => {
    progressionChordsRef.current = progressionChords;
  }, [progressionChords]);
  // Chord-walk mode: the baked cyclic walk read by the scheduler (chord voicing per group) — the
  // seeded first cycle. With loop melody OFF the scheduler re-rolls a fresh legal cycle each loop
  // (closure preserved: generateChordWalk always returns to origin); with loop melody ON this seeded
  // walk is frozen and repeated. It re-bakes on Generate / heuristic, seeding the next first cycle.
  const chordWalkStepsRef = useRef(chordWalkSteps);
  useEffect(() => {
    chordWalkStepsRef.current = chordWalkSteps;
  }, [chordWalkSteps]);
  // Perception-walk: the baked first cycle and its resolved loop origin, mirrored so the scheduler
  // re-loops around that origin (default) or, in free roam, wanders from a carry chord seeded at the
  // origin. Re-baked on Generate / pitch change.
  const perceptionWalkStepsRef = useRef(perceptionWalkSteps);
  const perceptionOriginRef = useRef<number[] | null>(perceptionWalkRaw?.origin ?? null);
  useEffect(() => {
    perceptionWalkStepsRef.current = perceptionWalkSteps;
    perceptionOriginRef.current = perceptionWalkRaw?.origin ?? null;
  }, [perceptionWalkSteps, perceptionWalkRaw]);
  // The curated placement pool and heuristic chord vocabulary, mirrored into refs so a running loop's
  // per-cycle regeneration (in play's pump) picks up a mid-play heuristic / set switch live, exactly
  // like chordWalkStepsRef. generateChordWalk reads both when re-rolling a fresh cycle.
  const curatedPoolRef = useRef(curatedPool);
  useEffect(() => {
    curatedPoolRef.current = curatedPool;
  }, [curatedPool]);
  const walkCandidatesRef = useRef(walkCandidates);
  useEffect(() => {
    walkCandidatesRef.current = walkCandidates;
  }, [walkCandidates]);
  // The phrase→walk bindings, mirrored so the per-cycle re-roll honors a mid-play bind-mode / phrase
  // change live (like the pool/candidate refs). generateWalkRelaxed reads it each re-roll.
  const walkBindingsRef = useRef(walkBindings);
  useEffect(() => {
    walkBindingsRef.current = walkBindings;
  }, [walkBindings]);
  // The parsed phrase and the melody-bind flag, mirrored so the scheduler's per-cycle melody bake
  // (loop-off parity) honors a mid-play phrase / checkbox change live, like the walk refs above.
  const phraseRepeatsRef = useRef(phraseRepeats);
  useEffect(() => {
    phraseRepeatsRef.current = phraseRepeats;
  }, [phraseRepeats]);
  const bindMelodyRef = useRef(bindMelody);
  useEffect(() => {
    bindMelodyRef.current = bindMelody;
  }, [bindMelody]);
  // Guided mode: keep chordState (the voicing source) synced to the selected preset chord,
  // so switching the chord dropdown re-voices the accompaniment. `matches` stays empty —
  // guided mode never uses the superset-match dropdown.
  useEffect(() => {
    if (!guided) return;
    const c = presets![selectedChordIdx];
    if (c) setChordState({keys: c.keys, matches: []});
  }, [guided, presets, selectedChordIdx]);
  useEffect(() => {
    octaveKeysRef.current = octaveKeys;
  }, [octaveKeys]);
  useEffect(() => {
    bakedKeysRef.current = bakedKeys;
  }, [bakedKeys]);
  useEffect(() => {
    loopMelodyRef.current = loopMelody;
  }, [loopMelody]);
  useEffect(() => {
    loopChordsRef.current = loopChords;
  }, [loopChords]);
  useEffect(() => {
    freeRoamRef.current = freeRoam;
  }, [freeRoam]);
  useEffect(() => {
    melodyOnRef.current = melodyOn;
  }, [melodyOn]);
  useEffect(() => {
    isRandomPitchRef.current = isRandomPitch;
  }, [isRandomPitch]);

  // Spectrum colours for the bars. With a family selected, tint each firing bar by its
  // baked pitch (deterministic per pattern/seed) — this is the loop-on colouring and the
  // pre-play preview for loop-off (the live scheduler overwrites those per hit). Without a
  // family (the non-melody player), leave the refs null so the bars stay flat blue.
  useEffect(() => {
    if (!pattern) return;
    if (!melodyOn || !bakedKeys) {
      fillColorsRef.current = null;
      strokeColorsRef.current = null;
    } else {
      const n = pattern.pulses.length;
      const fills = Array<string>(n).fill(BLUE_FILL);
      const strokes = Array<string>(n).fill(BLUE_STROKE);
      const idx = firingPulseIndices(pattern.pulses);
      bakedKeys.forEach((key, e) => {
        fills[idx[e]] = pitchFill(key);
        strokes[idx[e]] = pitchStroke(key);
      });
      fillColorsRef.current = fills;
      strokeColorsRef.current = strokes;
    }
    // redraw(true, …) rebuilds the bar paths so the disp colour callbacks are re-read; a
    // bare redraw(false, …) would only repaint cached paths and ignore the new colours.
    // setScale=false keeps the fixed axis ranges. Handles family changes without a
    // regenerate (the plot is only recreated on a new pattern).
    plotRef.current?.redraw(true, false);
    // loopMelody is a dep so flipping loop back on mid-play restores the baked colours the
    // live loop-off scheduler had overwritten (the body always paints the baked preview).
  }, [pattern, melodyOn, octaveKeys, bakedKeys, loopMelody]);

  // --- Parameter editing (does NOT regenerate the pattern; only Generate does) ---

  // Re-parse the current phrase text against a (possibly new) meter/subdivisions — a group-count
  // or subdivision change can invalidate a phrase. Empty text clears repetition without error.
  const revalidatePhrase = (m: number[], s: number[]) => {
    try {
      setPhraseRepeats(
        phraseText.trim().length === 0 ? null : parsePhrase(phraseText, m, s),
      );
      setPhraseError(null);
    } catch (e) {
      setPhraseError((e as Error).message);
    }
  };

  const onMeterText = (text: string) => {
    setMeterText(text);
    try {
      const m = parseMeter(text);
      setMeter(m);
      setMeterError(null);
      // A new group count can invalidate a per-group subdivision list — re-check it.
      let s = subdivisions;
      try {
        s = parseSubdivisions(subText, m.length);
        setSubdivisions(s);
        setSubError(null);
      } catch (e) {
        setSubError((e as Error).message);
      }
      // The phrase's validity depends on the meter/subdivisions too — re-check it.
      revalidatePhrase(m, s);
    } catch (e) {
      setMeterError((e as Error).message);
    }
  };

  const onSubText = (text: string) => {
    setSubText(text);
    try {
      const s = parseSubdivisions(text, meter.length);
      setSubdivisions(s);
      setSubError(null);
      revalidatePhrase(meter, s);
    } catch (e) {
      setSubError((e as Error).message);
    }
  };

  // Phrase entry (phrase mode): parse the scheme against the current meter/subdivisions and make
  // it the active repetition. Empty clears it (fully-random per group). Malformed / incompatible
  // input is an error that keeps the last valid scheme.
  const onPhraseText = (text: string) => {
    setPhraseText(text);
    try {
      setPhraseRepeats(
        text.trim().length === 0 ? null : parsePhrase(text, meter, subdivisions),
      );
      setPhraseError(null);
    } catch (e) {
      setPhraseError((e as Error).message);
    }
  };

  // Chord entry (chord mode): find the typed chord's LCM ≤ 24 superset placements and make it
  // the sounding chord. A chord with no match is still valid — it sounds as accompaniment
  // (driven by chordState.keys) with the melody off. Only malformed input (bad keys / wrong
  // count) is an error, which keeps the last valid chord.
  const onChordText = (text: string) => {
    setChordText(text);
    try {
      const keys = parseChordKeys(text);
      setChordState({keys, matches: findSupersets(keys, 12, MAX_CHORD_LCM)});
      setSelectedMatchIdx(0);
      setChordError(null);
    } catch (e) {
      setChordError((e as Error).message);
    }
  };

  // Number-box entry for the two [0,1] sliders. Keep the raw text while editing so
  // decimals type smoothly; update the numeric value (clamped) whenever it parses.
  const onSyncText = (text: string) => {
    setSyncText(text);
    const v = parseFloat(text);
    if (!Number.isNaN(v)) setSyncopation(clampUnit(v));
  };
  const onResText = (text: string) => {
    setResText(text);
    const v = parseFloat(text);
    if (!Number.isNaN(v)) setResolution(clampUnit(v));
  };

  const hasError = meterError !== null || subError !== null;

  // --- Audio ---

  const getSynth = () => {
    if (!synthRef.current) {
      gainRef.current = new Tone.Gain(0.55).toDestination(); // master headroom
      synthRef.current = new Tone.Synth({
        oscillator: {type: 'triangle'},
        // Percussive blip with a short body: a small sustain lets the per-hit duration
        // (set from velocity below) actually change the blip length, not just its volume.
        envelope: {attack: 0.001, decay: 0.04, sustain: 0.3, release: 0.04},
      }).connect(gainRef.current);
    }
    return synthRef.current;
  };

  // Lazily build the chord synth: a soft sine PolySynth, mixed well below the melody blips
  // so they stay audible over it. A short release keeps each meter group's chord a distinct
  // long note (re-articulated per group) rather than washing into a continuous pad. Only
  // used in chord mode.
  const getChordSynth = () => {
    if (!chordSynthRef.current) {
      chordGainRef.current = new Tone.Gain(0.12).toDestination();
      chordSynthRef.current = new Tone.PolySynth(Tone.Synth, {
        oscillator: {type: 'sine'},
        envelope: {attack: 0.03, decay: 0.15, sustain: 0.7, release: 0.5},
      }).connect(chordGainRef.current);
    }
    return chordSynthRef.current;
  };

  // Lazily build the sampled-piano melody voice: a Tone.Sampler over the C-per-octave
  // samples, on its own persistent gain node (kept for the component's life). Same
  // triggerAttackRelease(freqHz, durSec, at, vel) surface as the sine synths, so the
  // scheduler treats it identically. Samples fetch on first build; the caller awaits load.
  const getPianoMelody = () => {
    if (!pianoMelodyRef.current) {
      pianoMelodyGainRef.current = new Tone.Gain(0.5).toDestination();
      pianoMelodyRef.current = new Tone.Sampler({
        urls: PIANO_URLS,
        baseUrl: sampleBase,
        release: 0.8,
      }).connect(pianoMelodyGainRef.current);
    }
    return pianoMelodyRef.current;
  };

  // Lazily build the sampled-piano chord voice — same Sampler, its own gain node mixed a
  // touch below the melody so the blips stay audible over the pad. Only used in chord mode.
  const getPianoChord = () => {
    if (!pianoChordRef.current) {
      pianoChordGainRef.current = new Tone.Gain(0.4).toDestination();
      pianoChordRef.current = new Tone.Sampler({
        urls: PIANO_URLS,
        baseUrl: sampleBase,
        release: 0.8,
      }).connect(pianoChordGainRef.current);
    }
    return pianoChordRef.current;
  };

  const stop = () => {
    if (loopTimerRef.current) {
      clearInterval(loopTimerRef.current);
      loopTimerRef.current = null;
    }
    synthRef.current?.dispose(); // cancels future scheduled clicks + cuts the voice
    synthRef.current = null; // a disposed synth can't retrigger; rebuild next play
    gainRef.current?.dispose();
    gainRef.current = null;
    chordSynthRef.current?.dispose(); // cut the sounding chord too
    chordSynthRef.current = null;
    chordGainRef.current?.dispose();
    chordGainRef.current = null;
    // Piano voices are cached across Stop/Play (see builders); just cut any ringing notes
    // rather than disposing, so the next Play reuses the loaded samples without re-fetching.
    pianoMelodyRef.current?.releaseAll();
    pianoChordRef.current?.releaseAll();
    // Invalidate in-flight Draw callbacks (up to the ~0.3 s look-ahead) and hide the marker.
    playRunRef.current++;
    playheadBeatRef.current = null;
    plotRef.current?.redraw(false, false);
    // Keep the last chord/placement readouts on Stop so they can be inspected
    // while paused (generate() clears them for a fresh pattern; see below).
    setPlaying(false);
  };

  useEffect(
    () => () => {
      stop();
      // Piano voices survive Stop; tear them (and their gains) down for good on unmount.
      pianoMelodyRef.current?.dispose();
      pianoMelodyGainRef.current?.dispose();
      pianoChordRef.current?.dispose();
      pianoChordGainRef.current?.dispose();
    },
    [],
  ); // dispose on unmount

  const play = async () => {
    if (!pattern) return;
    await Tone.start(); // unlock audio on the user gesture
    // Pick the active voices from the current instrument. Piano builds its samplers on first
    // use and fetches samples; gate playback on Tone.loaded() so the first note isn't silent.
    const usePiano = instrument === 'piano';
    const chordEngine = chord || progressionOn || chordWalkOn || perceptionWalkOn;
    if (usePiano) {
      getPianoMelody();
      if (chordEngine) getPianoChord();
      setPianoLoading(true);
      try {
        await Tone.loaded();
      } finally {
        setPianoLoading(false);
      }
    }
    const synth = usePiano ? getPianoMelody() : getSynth();
    const chordSynth = chordEngine
      ? usePiano
        ? getPianoChord()
        : getChordSynth()
      : null;
    const {pulses, totalBeats, meter: patternMeter} = pattern;
    // Chord onsets: map each meter group-start unit beat → that group's length in unit
    // beats (cumulative sums over the meter, as in gridLines' groupStarts). The chord
    // re-strikes at each group start, held for its group's length. Group starts are integer
    // on-beats that always fire, so exact key lookup against a firing event's unitBeat is
    // safe and every group start is reached by the events/i iteration below. groupIdxByStart
    // maps the same starts to their 0-based group index, so progression mode can pick that
    // group's baked triad voicing.
    const groupBeatsByStart = new Map<number, number>();
    const groupIdxByStart = new Map<number, number>();
    let groupAcc = 0;
    patternMeter.forEach((m, gi) => {
      groupBeatsByStart.set(groupAcc, m);
      groupIdxByStart.set(groupAcc, gi);
      groupAcc += m;
    });
    // Chord-walk mode: the walk re-rolls per loop (unless loop melody is on), so keep it cycle-local
    // rather than static. `cycleSteps` is the current cycle's baked walk (chord voicing + per-group
    // melody pools); `cycleWalkPools` is the folded melody pool per meter group. Both are seeded with
    // the baked first cycle and refreshed at each full pass in pump(); `walkStarts` buckets an event
    // into its meter group. Precompute the meter length and origin for the per-cycle regeneration.
    const walkOn = chordWalkOn || perceptionWalkOn;
    const walkMeterLen = patternMeter.length;
    const originKeys = chordWalkOn
      ? foldOctave(chordWalk!.origin)
      : (perceptionOriginRef.current ?? []);
    let cycleSteps = perceptionWalkOn
      ? perceptionWalkStepsRef.current
      : chordWalkOn
        ? chordWalkStepsRef.current
        : null;
    let cycleWalkPools = cycleSteps?.map((s) => s.melodyKeys) ?? null;
    // Free-roam carry: the chord the *next* loop pass starts from. Both walks seed it at the origin
    // and only advance it in free-roam (the default closed loop always returns to the origin, so the
    // carry stays put); see the re-roll block below.
    let carryChord = originKeys;
    const walkStarts = walkOn ? groupStartBeats(patternMeter) : null;
    // Per-group pulse ranges, for the per-cycle melody bake that gives melody binding loop-off
    // parity: `cycleMelody` is this cycle's bound melody (one key per firing event) when melody
    // binding is on, else null (each hit re-rolls live). Refreshed at each cycle boundary below.
    const walkRanges = walkOn
      ? groupPulseRanges(patternMeter, pattern.subdivisions)
      : null;
    let cycleMelody: number[] | null = null;
    // Only firing pulses become onsets, sorted by position within the cycle.
    const events = firingEvents(pulses);
    // Bar index of each onset, aligned with `events`, for live loop-off recolouring.
    const firingPulseIdx = firingPulseIndices(pulses);
    // Perception-walk: the firing-event ordinal that opens each meter group (its earliest onset),
    // so the scheduler can force that note to the group's opening key. Empty in other modes.
    const groupFirstEvent = new Map<number, number>();
    if (perceptionWalkOn && walkStarts)
      events.forEach((ev, e) => {
        const gi = groupIndexOf(ev.unitBeat, walkStarts);
        if (!groupFirstEvent.has(gi)) groupFirstEvent.set(gi, e);
      });
    const N = events.length;
    if (N === 0) return;
    const runId = ++playRunRef.current; // tags this run; stale Draw callbacks no-op
    setPlaying(true);

    // Continuous look-ahead loop copied from SequencePlayerClient: schedule each onset
    // a little ahead of the audio clock, anchored on the last onset actually queued, so
    // a live tempo change stretches the rhythm within the cycle instead of at its edge.
    const lookAheadSec = 0.3;
    const t0 = Tone.now() + 0.06;
    let i = 0;
    let prevBeat = 0;
    let prevTime = t0;
    const pump = () => {
      const sec = tempoRef.current; // seconds per unit beat, read live
      for (;;) {
        const ev = events[i % N];
        const absBeat = Math.floor(i / N) * totalBeats + ev.unitBeat;
        const at = prevTime + (absBeat - prevBeat) * sec;
        if (at >= Tone.now() + lookAheadSec) break; // not due yet — recompute next poll
        // Chord-walk: `i % N === 0` marks the start of each full pass = one walk cycle. Re-roll a
        // fresh legal cycle then, so successive loops walk different placements — UNLESS loop melody
        // is on (freeze the seeded walk so its baked phrase stays coherent) or this is the first
        // cycle (keep Generate deterministic per seed). Free roam swaps the closed re-roll (always
        // returns to origin) for an OPEN walk that continues from the previous pass's ending chord
        // (carryChord), so the harmony wanders indefinitely; loop melody still freezes, so roam has
        // no effect there. Both honor the phrase bindings and refresh the "binding can't close" note.
        if (walkOn && walkMeterLen > 1 && i % N === 0) {
          if (perceptionWalkOn) {
            const roam = freeRoamRef.current && !loopMelodyRef.current;
            // Perception-walk: use the baked first cycle on pass 0 (seeded for Generate determinism)
            // or whenever loop melody freezes it. Otherwise, in free roam, generate a fresh OPEN walk
            // continuing from the carry chord (its ending chord seeds the next pass); by default,
            // re-roll a fresh CLOSED loop around the same origin so every pass returns to it.
            if (i === 0 || loopMelodyRef.current) {
              cycleSteps = perceptionWalkStepsRef.current;
            } else if (roam) {
              const {steps, next} = generatePerceptionWalk(
                carryChord,
                walkMeterLen,
                (Math.random() * 2 ** 32) >>> 0,
              );
              carryChord = next;
              cycleSteps = bakePerceptionSteps(steps, pitchHz);
            } else {
              const {steps} = generatePerceptionLoop(
                perceptionOriginRef.current,
                walkMeterLen,
                (Math.random() * 2 ** 32) >>> 0,
              );
              cycleSteps = bakePerceptionSteps(steps, pitchHz);
            }
          } else {
            const roam = freeRoamRef.current && !loopMelodyRef.current;
            const havePool = curatedPoolRef.current && walkCandidatesRef.current;
            if (roam && havePool) {
              // Free roam: open walk from the origin on the first pass (seeded for Generate
              // determinism), else continuing from the carry chord (live re-roll). The walk's ending
              // chord becomes the next pass's start, chaining passes into one continuous wander.
              const start = i === 0 ? originKeys : carryChord;
              const walkSeed = i === 0 ? seed ^ 0x2545f491 : (Math.random() * 2 ** 32) >>> 0;
              const {walk, next, unsatisfied} = generateOpenWalkRelaxed(
                start,
                walkCandidatesRef.current!,
                curatedPoolRef.current!,
                walkMeterLen,
                walkSeed,
                walkBindingsRef.current,
              );
              setBindingUnsatisfied(unsatisfied);
              carryChord = next;
              cycleSteps = bakeWalkSteps(walk, pitchHz);
            } else if (i === 0 || loopMelodyRef.current || !havePool) {
              cycleSteps = chordWalkStepsRef.current;
            } else {
              const {walk, unsatisfied} = generateWalkRelaxed(
                originKeys,
                walkCandidatesRef.current!,
                curatedPoolRef.current!,
                walkMeterLen,
                (Math.random() * 2 ** 32) >>> 0,
                walkBindingsRef.current,
              );
              setBindingUnsatisfied(unsatisfied);
              cycleSteps = bakeWalkSteps(walk, pitchHz);
            }
          }
          cycleWalkPools = cycleSteps?.map((s) => s.melodyKeys) ?? null;
          // Melody binding (loop-off parity): bake this cycle's melody so a repeated group replays
          // the group it repeats's motif note-for-note; unbound events draw fresh from their pool.
          // Loop-melody-on uses the frozen bakedKeys instead (handled in the per-hit draw below).
          cycleMelody =
            bindMelodyRef.current && walkRanges && phraseRepeatsRef.current
              ? bakeMelody(
                  firingPulseIdx,
                  walkRanges,
                  cycleWalkPools ?? [],
                  octaveKeysRef.current,
                  phraseRepeatsRef.current,
                  true,
                  Math.random,
                )
              : null;
          // Perception-walk: open each group on its opening key in the bound melody too (the
          // live loop-off path applies the same override at the draw site below).
          if (perceptionWalkOn && cycleMelody && walkRanges && cycleSteps)
            cycleMelody = applyOpeningKeys(cycleMelody, firingPulseIdx, walkRanges, cycleSteps);
        }
        // Chord: re-strike the whole voicing at each meter group start, held for that
        // group's length (in live-tempo seconds) so it tracks tempo changes — a slow
        // harmonic pulse beneath the faster melody. Offsets are the precomputed
        // lowest-penalty voicing, rooted an octave below the melody. In progression mode each
        // group gets a triad: the baked one (loop chords on) or a fresh random draw from the
        // set's minor-second-free pool (loop off); otherwise it is the single static chord
        // (chordVoicingRef). displayTriad feeds the "playing" readout via the Draw callback.
        const groupBeats = groupBeatsByStart.get(ev.unitBeat);
        let displayTriad: number[] | null = null;
        let displayPlacement: string | null = null;
        if (chordSynth && groupBeats !== undefined) {
          let offsets: number[] | null;
          if (progressionOn) {
            const gi = groupIdxByStart.get(ev.unitBeat)!;
            const pool = progressionTriadsRef.current;
            const groupChord =
              loopChordsRef.current || !pool || pool.length === 0
                ? (progressionChordsRef.current?.[gi] ?? null)
                : (() => {
                    const triad = pool[Math.floor(Math.random() * pool.length)];
                    return {triad, offsets: chordOffsets(triad, pitchHz)};
                  })();
            offsets = groupChord?.offsets ?? null;
            displayTriad = groupChord?.triad ?? null;
          } else if (walkOn) {
            // Use this group's step from the current cycle's walk (chord-walk re-rolls a closed
            // cycle per loop; perception-walk wanders open — both re-baked at the pass boundary).
            const gi = groupIdxByStart.get(ev.unitBeat)!;
            const step = cycleSteps?.[gi] ?? null;
            offsets = step?.offsets ?? null;
            displayTriad = step?.triad ?? null;
            displayPlacement = step?.placementLabel ?? null;
          } else {
            offsets = chordVoicingRef.current;
          }
          if (offsets && offsets.length > 0) {
            const freqs = offsets.map((off) => pitchHz * Math.pow(2, off / 12));
            // Hold for most of the group but stop short of the next group start, leaving a
            // brief gap (like the melody's short blips) so each re-strike articulates
            // instead of butting up against the next and sounding continuous.
            chordSynth.triggerAttackRelease(freqs, groupBeats * sec * 0.95, at);
          }
        }
        const vel = ev.velocity / 127;
        const durSec = 0.03 + 0.12 * vel; // heavier accents are both louder and longer
        // Pitch: the fixed pitchHz unless a melody is active, in which case draw a key —
        // baked (a repeating phrase) or fresh per hit — placed above the root pitchHz
        // (key 0) via the 12-TET ratio 2^(key/12). A family draws a discrete key from its
        // octave pool; random pitch draws a continuous key in [0,12).
        // The loop-off draw pool: normally the single global pool, but in chord-walk mode this
        // event's meter-group bridging pool (so the melody modulates group-to-group). Falls back
        // to the global pool for a group whose bridging pool is empty.
        let okeys = octaveKeysRef.current;
        if (cycleWalkPools && walkStarts) {
          const gp = cycleWalkPools[groupIndexOf(ev.unitBeat, walkStarts)];
          if (gp && gp.length) okeys = gp;
        }
        let freq = pitchHz;
        // Loop-off re-rolls each hit; capture that key so the Draw callback can light up this
        // bar's spectrum colour when it sounds (loop-on / non-melody leave colourKey null).
        let colourKey: number | null = null;
        if (melodyOnRef.current) {
          const baked = bakedKeysRef.current;
          const loopOn = loopMelodyRef.current && baked;
          // Loop on → the frozen baked phrase. Else, if this cycle's melody is bound, replay it
          // (so repeated groups restate their motif); otherwise re-roll a fresh pitch per hit.
          let key = loopOn
            ? baked![i % N]
            : cycleMelody
              ? cycleMelody[i % N]
              : isRandomPitchRef.current
                ? Math.random() * 12
                : okeys![Math.floor(Math.random() * okeys!.length)];
          // Perception-walk: if this event opens its meter group, sound the group's opening key
          // (the note that selected the placement/perception) instead of a drawn pitch. Overrides
          // every path — baked, bound, and live — so the perception is always articulated first.
          if (perceptionWalkOn && cycleSteps && walkStarts) {
            const gi = groupIndexOf(ev.unitBeat, walkStarts);
            const ok = cycleSteps[gi]?.openingKey;
            if (groupFirstEvent.get(gi) === i % N && ok != null) key = ((ok % 12) + 12) % 12;
          }
          freq = pitchHz * Math.pow(2, key / 12);
          if (!loopOn) colourKey = key;
        }
        // One Draw callback per onset, fired exactly when it sounds: move the sing-along
        // playhead to this bar (all players) and, for loop-off melody, recolour it live.
        const bar = firingPulseIdx[i % N];
        const beat = ev.unitBeat;
        const isGroupStart = groupBeats !== undefined;
        Tone.getDraw().schedule(() => {
          if (playRunRef.current !== runId || !plotRef.current) return; // stale run / no plot
          playheadBeatRef.current = beat;
          // At a meter group start in progression mode, surface the sounding triad in the
          // "playing" readout (a slow, at-most-per-group state update).
          if (isGroupStart && (progressionOn || walkOn)) setCurrentChord(displayTriad);
          if (isGroupStart && walkOn) setCurrentPlacement(displayPlacement);
          if (colourKey != null) {
            (fillColorsRef.current ??= Array<string>(pulses.length).fill(BLUE_FILL))[bar] =
              pitchFill(colourKey);
            (strokeColorsRef.current ??= Array<string>(pulses.length).fill(BLUE_STROKE))[
              bar
            ] = pitchStroke(colourKey);
          }
          // Rebuild paths only when a colour changed (so disp re-reads); otherwise a cheap
          // repaint that still re-runs the playhead draw hook. setScale=false keeps the axes.
          plotRef.current.redraw(colourKey != null, false);
        }, at);
        synth.triggerAttackRelease(freq, durSec, at, vel);
        prevTime = at;
        prevBeat = absBeat;
        i++;
      }
    };
    pump();
    loopTimerRef.current = setInterval(pump, 80); // poll well inside the look-ahead
  };

  // --- Generation (the one place the concrete pattern changes) ---

  const regenerate = (nextSeed: number) => {
    const p = generatePattern(
      {meter, subdivisions, syncopation, resolution, repeats: phraseRepeats ?? undefined},
      nextSeed,
    );
    setPattern({...p, meter, subdivisions});
  };

  // Chord mode: roll a fresh chord + its superset matches, defaulting the melody to the
  // tightest match (fewest extra keys — matches are ranked tightest-first). The dropdown can
  // then switch among the rest to hear the ambiguity.
  const rollNewChord = () => {
    const {keys, matches} = rollChord(Math.random);
    setChordState({keys, matches});
    setSelectedMatchIdx(0);
    // Mirror the rolled chord into the editable box (and clear any stale error).
    setChordText(keys.join(' '));
    setChordError(null);
  };

  const generate = () => {
    stop();
    // A new pattern makes the old readouts misleading, so clear them here
    // (Stop itself now keeps them for inspection).
    setCurrentChord(null);
    setCurrentPlacement(null);
    const nextSeed = (seed + 1) >>> 0;
    setSeed(nextSeed);
    regenerate(nextSeed);
  };

  // First mount: seed the visual so it isn't empty (uses the initial seed=1), and in random
  // chord mode roll the first chord so the player has a melody family to draw from immediately.
  // Guided mode instead seeds chordState from the first preset (see the guided-sync effect).
  useEffect(() => {
    if (chord && !guided) rollNewChord();
    regenerate(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // --- Visualization: rebuild the bar plot whenever a new pattern is generated ---

  useEffect(() => {
    const container = containerRef.current;
    if (!container || !pattern) return;
    const {pulses, totalBeats, meter: pm, subdivisions: ps} = pattern;
    const xs = Float64Array.from(pulses, (p) => p.unitBeat);
    const ys = Float64Array.from(pulses, (p) => p.velocity);
    const lines = gridLines(pm, ps);
    const width = container.clientWidth || 700;

    const opts: uPlot.Options = {
      width,
      height,
      legend: {show: false},
      cursor: {show: false},
      scales: {
        x: {time: false, range: () => [-0.2, totalBeats + 0.2]},
        y: {range: () => [0, 127]},
      },
      axes: [
        {label: 'position (unit beats)', splits: () => lines.unitBeats},
        {label: 'velocity (0–127)', splits: () => [0, 32, 64, 96, 127]},
      ],
      series: [
        {},
        {
          label: 'velocity',
          stroke: BLUE_STROKE,
          fill: BLUE_FILL,
          // Per-bar spectrum colours via disp (unit 3 = Color): the values callbacks read
          // the live colour refs each draw, so a cheap redraw() repaints without a rebuild.
          // Null refs (the non-melody player) fall back to flat blue for every bar.
          paths: uPlot.paths.bars!({
            size: [0.55, 16],
            align: 0,
            disp: {
              fill: {
                unit: 3,
                values: (u) =>
                  fillColorsRef.current ??
                  Array<string>(u.data[0].length).fill(BLUE_FILL),
              },
              stroke: {
                unit: 3,
                values: (u) =>
                  strokeColorsRef.current ??
                  Array<string>(u.data[0].length).fill(BLUE_STROKE),
              },
            },
          }),
          points: {show: false},
        },
      ],
      plugins: [gridPlugin(lines), playheadPlugin(playheadBeatRef)],
    };

    const u = new uPlot(opts, [xs, ys] as uPlot.AlignedData, container);
    plotRef.current = u;

    const ro = new ResizeObserver(() => {
      u.setSize({width: container.clientWidth, height});
    });
    ro.observe(container);

    return () => {
      ro.disconnect();
      u.destroy();
      plotRef.current = null;
    };
  }, [pattern, height]);

  const maxSub = Math.max(1, ...subdivisions);
  const fastestMs = Math.round((60 / bpm / maxSub) * 1000);

  const rangeStyle = {width: '10rem', verticalAlign: 'middle'} as const;
  const labelStyle = {
    display: 'flex',
    alignItems: 'center',
    gap: '0.4rem',
  } as const;

  return (
    <div style={{margin: '1rem 0'}}>
      <div style={{position: 'relative'}}>
        <div ref={containerRef} style={{width: '100%', minHeight: height}} />
        {/* Spectrum legend — only when pitch colouring is active (a family or random pitch).
            The uPlot container mutates its own DOM, so this overlay is a sibling, not a child. */}
        {melodyOn && (
          <div
            style={{
              position: 'absolute',
              top: 10,
              right: 12,
              pointerEvents: 'none',
              display: 'flex',
              alignItems: 'center',
              gap: '0.35rem',
              fontSize: '0.7rem',
              color: 'var(--ifm-font-color-base)',
              background: 'var(--ifm-background-surface-color)',
              borderRadius: '4px',
              padding: '0.2rem 0.4rem',
              boxShadow: '0 1px 4px rgba(0,0,0,0.25)',
            }}>
            <span style={{opacity: 0.75}}>pitch</span>
            <span style={{opacity: 0.85}}>low</span>
            <span
              style={{
                display: 'inline-block',
                width: 72,
                height: 9,
                borderRadius: '3px',
                background: SPECTRUM_GRADIENT,
              }}
            />
            <span style={{opacity: 0.85}}>high</span>
          </div>
        )}
      </div>
      <div
        style={{
          fontSize: '0.8rem',
          opacity: 0.75,
          marginTop: '0.25rem',
          display: 'flex',
          gap: '1rem',
          flexWrap: 'wrap',
        }}>
        <span>
          <b>bold</b> = meter accent
        </span>
        <span>medium = unit beat</span>
        <span>faint = pulse</span>
        <span>
          · {bpm} BPM · fastest pulse {fastestMs} ms
        </span>
      </div>

      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '1rem 1.4rem',
          alignItems: 'center',
          marginTop: '0.7rem',
          fontSize: '0.9rem',
        }}>
        <label style={labelStyle}>
          meter
          <input
            type="text"
            value={meterText}
            onChange={(e) => onMeterText(e.target.value)}
            style={{width: '6rem'}}
            aria-label="meter groups"
          />
        </label>
        <label style={labelStyle}>
          subdivision
          <input
            type="text"
            value={subText}
            onChange={(e) => onSubText(e.target.value)}
            style={{width: '6rem'}}
            aria-label="subdivision per group"
          />
        </label>
        {phrases && (
          <label style={labelStyle}>
            phrase
            <input
              type="text"
              value={phraseText}
              onChange={(e) => onPhraseText(e.target.value)}
              placeholder="e.g. ABAC"
              style={{width: '7rem'}}
              aria-label="phrase repetition scheme"
            />
          </label>
        )}
        <label style={labelStyle}>
          tempo
          <input
            type="range"
            min={minBpm}
            max={maxBpm}
            step={1}
            value={bpm}
            onChange={(e) => setBpmBoth(parseFloat(e.target.value))}
            style={rangeStyle}
          />
          <input
            type="number"
            min={minBpm}
            max={maxBpm}
            value={bpm}
            onChange={(e) => {
              const v = parseFloat(e.target.value);
              if (!Number.isNaN(v)) setBpmBoth(v);
            }}
            style={{width: '4.5rem'}}
            aria-label="tempo in BPM"
          />
          <code>BPM</code>
        </label>
        <label style={labelStyle}>
          syncopation
          <input
            type="range"
            min={0}
            max={1}
            step={0.01}
            value={syncopation}
            onChange={(e) => {
              const v = clampUnit(parseFloat(e.target.value));
              setSyncopation(v);
              setSyncText(fmtUnit(v));
            }}
            style={rangeStyle}
          />
          <input
            type="text"
            inputMode="decimal"
            value={syncText}
            onChange={(e) => onSyncText(e.target.value)}
            onBlur={() => setSyncText(fmtUnit(syncopation))}
            style={{width: '4.5rem'}}
            aria-label="syncopation amount"
          />
        </label>
        <label style={labelStyle}>
          resolution
          <input
            type="range"
            min={0}
            max={1}
            step={0.01}
            value={resolution}
            onChange={(e) => {
              const v = clampUnit(parseFloat(e.target.value));
              setResolution(v);
              setResText(fmtUnit(v));
            }}
            style={rangeStyle}
          />
          <input
            type="text"
            inputMode="decimal"
            value={resText}
            onChange={(e) => onResText(e.target.value)}
            onBlur={() => setResText(fmtUnit(resolution))}
            style={{width: '4.5rem'}}
            aria-label="resolution amount"
          />
        </label>
        <label style={labelStyle}>
          instrument
          <select
            value={instrument}
            onChange={(e) => setInstrument(e.target.value as 'sine' | 'piano')}
            aria-label="instrument timbre">
            <option value="sine">Sine</option>
            <option value="piano">Piano</option>
          </select>
          {pianoLoading && (
            <span style={{opacity: 0.7, fontStyle: 'italic'}}>loading…</span>
          )}
        </label>
        {melody && !chord && (
          <label style={labelStyle}>
            lcm
            <select
              value={selectedLcm}
              onChange={(e) => setSelectedLcm(e.target.value)}
              aria-label="lcm family for melody pitches">
              {LCM_FAMILIES.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.label}
                </option>
              ))}
            </select>
          </label>
        )}
        {progressionOn && (
          <>
            <label style={labelStyle}>
              set
              <select
                value={selectedProgIdx}
                onChange={(e) => setSelectedProgIdx(Number(e.target.value))}
                aria-label="source set for chords and melody">
                {progression!.map((p, i) => (
                  <option key={i} value={i}>
                    {p.label}
                  </option>
                ))}
              </select>
            </label>
            <label
              style={{
                ...labelStyle,
                // A pinned set ignores the heuristic — dim and disable the control to show it.
                opacity: progression![selectedProgIdx].chord?.length ? 0.4 : 1,
              }}>
              progression
              <select
                value={selectedHeuristicIdx}
                onChange={(e) => setSelectedHeuristicIdx(Number(e.target.value))}
                disabled={(progression![selectedProgIdx].chord?.length ?? 0) > 0}
                aria-label="progression heuristic">
                {PROGRESSION_HEURISTICS.map((h, i) => (
                  <option key={h.id} value={i}>
                    {h.label}
                  </option>
                ))}
              </select>
            </label>
            <span style={labelStyle}>
              playing
              <code>{currentChord ? currentChord.join(' ') : '—'}</code>
            </span>
          </>
        )}
        {chordWalkOn && (
          <>
            <label style={labelStyle}>
              chords
              <select
                value={walkHeuristicIdx}
                onChange={(e) => setWalkHeuristicIdx(Number(e.target.value))}
                aria-label="chord vocabulary for the walk">
                {PROGRESSION_HEURISTICS.map((h, i) => (
                  <option key={h.id} value={i}>
                    {h.label}
                  </option>
                ))}
              </select>
            </label>
            {phrases && (
              <span style={{...labelStyle, gap: '0.6rem'}}>
                phrase binds
                {PHRASE_BIND_AXES.map((axis) => {
                  const checked =
                    axis.id === 'chords'
                      ? bindChords
                      : axis.id === 'placements'
                        ? bindPlacements
                        : bindMelody;
                  const setter =
                    axis.id === 'chords'
                      ? setBindChords
                      : axis.id === 'placements'
                        ? setBindPlacements
                        : setBindMelody;
                  return (
                    <label key={axis.id} style={labelStyle}>
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={(e) => setter(e.target.checked)}
                        aria-label={`bind ${axis.id} to the phrase`}
                      />
                      {axis.label}
                    </label>
                  );
                })}
              </span>
            )}
            <span style={{...labelStyle, gap: '0.6rem'}}>
              placements
              {chordWalk!.placements.map((p, i) => {
                const isLastEnabled =
                  enabledPlacements[i] && enabledPlacements.filter(Boolean).length === 1;
                return (
                  <label key={i} style={labelStyle}>
                    <input
                      type="checkbox"
                      checked={enabledPlacements[i]}
                      disabled={isLastEnabled}
                      onChange={() => togglePlacement(i)}
                      aria-label={`use placement ${placementPatternLabel(p)} in the walk`}
                    />
                    {placementPatternLabel(p)}
                  </label>
                );
              })}
            </span>
            <span style={labelStyle}>
              playing
              <code>{currentChord ? currentChord.join(' ') : '—'}</code>
            </span>
            <span style={labelStyle}>
              placement
              <code>{currentPlacement ?? '—'}</code>
            </span>
          </>
        )}
        {perceptionWalkOn && (
          <>
            <span style={labelStyle}>
              playing
              <code>{currentChord ? currentChord.join(' ') : '—'}</code>
            </span>
            <span style={labelStyle}>
              placement
              <code>{currentPlacement ?? '—'}</code>
            </span>
          </>
        )}
        {guided && (
          <>
            <label style={labelStyle}>
              chord
              <select
                value={selectedChordIdx}
                onChange={(e) => {
                  setSelectedChordIdx(Number(e.target.value));
                  setSelectedMelodyIdx(0); // its melody options swap with the chord
                }}
                aria-label="chord for the accompaniment">
                {presets!.map((c, i) => (
                  <option key={i} value={i}>
                    {c.label}
                  </option>
                ))}
              </select>
            </label>
            <label style={labelStyle}>
              melody
              <select
                value={selectedMelodyIdx}
                onChange={(e) => setSelectedMelodyIdx(Number(e.target.value))}
                aria-label="melody set to draw pitches from">
                {presets![selectedChordIdx].melodies.map((m, i) => (
                  <option key={i} value={i}>
                    {m.label}
                  </option>
                ))}
              </select>
            </label>
          </>
        )}
        {chord && !guided && chordState && (
          <>
            <label style={labelStyle}>
              chord
              <input
                type="text"
                value={chordText}
                onChange={(e) => onChordText(e.target.value)}
                style={{width: '7rem'}}
                aria-label="chord keys 0-11"
              />
            </label>
            {chordState.matches.length > 0 ? (
              <label style={labelStyle}>
                lcm
                <select
                  value={selectedMatchIdx}
                  onChange={(e) => setSelectedMatchIdx(Number(e.target.value))}
                  aria-label="matched lcm family for melody pitches">
                  {chordState.matches.map((m, i) => (
                    <option key={i} value={i}>
                      {matchLabel(m)}
                    </option>
                  ))}
                </select>
              </label>
            ) : (
              <span style={{...labelStyle, opacity: 0.7, fontStyle: 'italic'}}>
                no LCM ≤ 24 match
              </span>
            )}
          </>
        )}
        {(melody || chord || progressionOn || chordWalkOn || perceptionWalkOn) && (
          <label style={labelStyle}>
            <input
              type="checkbox"
              checked={loopMelody}
              onChange={(e) => setLoopMelody(e.target.checked)}
              aria-label="loop the drawn melody"
            />
            loop melody
          </label>
        )}
        {(chordWalkOn || perceptionWalkOn) && (
          <label style={{...labelStyle, opacity: loopMelody ? 0.5 : 1}}>
            <input
              type="checkbox"
              checked={freeRoam}
              disabled={loopMelody}
              onChange={(e) => setFreeRoam(e.target.checked)}
              aria-label="free roam: wander without returning to the origin chord"
            />
            free roam
          </label>
        )}
        {progressionOn && (
          <label style={labelStyle}>
            <input
              type="checkbox"
              checked={loopChords}
              onChange={(e) => setLoopChords(e.target.checked)}
              aria-label="loop the chord progression"
            />
            loop chords
          </label>
        )}
      </div>

      {(meterError || subError || phraseError || chordError) && (
        <div
          style={{
            color: 'var(--ifm-color-danger)',
            fontSize: '0.85rem',
            marginTop: '0.4rem',
          }}>
          {meterError ?? subError ?? phraseError ?? chordError}
        </div>
      )}

      {chordWalkOn && (bindChords || bindPlacements) && bindingUnsatisfied && (
        <div
          style={{
            color: 'var(--ifm-color-warning-dark)',
            fontSize: '0.85rem',
            marginTop: '0.4rem',
          }}>
          Phrase binding can’t close a cycle here — walking freely this loop.
        </div>
      )}

      <div style={{marginTop: '0.7rem', display: 'flex', gap: '0.6rem'}}>
        <button
          className="button button--primary button--sm"
          onClick={playing ? stop : play}
          disabled={pianoLoading}>
          {pianoLoading ? 'Loading…' : playing ? 'Stop' : 'Play'}
        </button>
        <button
          className="button button--secondary button--sm"
          onClick={generate}
          disabled={hasError}
          title={
            hasError
              ? 'Fix the meter / subdivision input first'
              : 'Stop and sample a new pattern'
          }>
          Generate Rhythm
        </button>
        {chord && !guided && (
          <button
            className="button button--secondary button--sm"
            onClick={rollNewChord}
            title="Roll a fresh random chord">
            Roll chord
          </button>
        )}
      </div>
    </div>
  );
}
