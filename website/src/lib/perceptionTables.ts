// Perception-based progression logic for the RhythmPatternPlayer. Each rhythmic unit (meter
// group) sounds a triad and draws its melody from a placement chosen by the unit's *opening
// key*, per a hand-authored perception table (see the "Perception Based Progression" section of
// docs/music/voicings-and-lcm-families.mdx). The next unit's chord is drawn from the current
// chord's *stable melodic supersets*. Pure (no React/Tone), mirroring chordWalk.ts / placements.ts;
// the tables are authored data (subjective perception), so unlike the placement math there is no
// C# oracle — the key-set constants and superset lists are transcribed from the doc.

import {placementKeys} from './placements';
import {mulberry32} from './rhythmPattern';

// Fold a key list into one octave of distinct pitch classes [0,12), sorted low → high. Same helper
// as chordWalk.ts, kept local to avoid coupling the two walk libs.
const foldOctave = (keys: number[]): number[] =>
  [...new Set(keys.map((k) => ((k % 12) + 12) % 12))].sort((a, b) => a - b);

// True iff every key of `chord` is in `set` (both folded/sorted pitch-class arrays).
const isSubset = (chord: number[], set: number[]): boolean => {
  const s = new Set(set);
  return chord.every((k) => s.has(k));
};

// --- Custom placement key sets (root-relative, anchor 0) ------------------------------------

// The non-LCM scales the perception tables reference, as folded base key sets at anchor 0.
// Transcribed from the doc: `15s` (stable-15 subset) and `blues` are the explicit sets used in the
// chordWalk example; `bluesExt` extends `blues` with key 10; `harm` is the harmonic minor scale
// (verified: harm rotated to anchor 5 = 0 1 4 5 7 8 10, the `harm@5` cited in the doc).
const LABEL_KEYS: Record<string, number[]> = {
  '15s': [0, 1, 3, 5, 9, 10],
  blues: [0, 2, 3, 4, 7, 9],
  bluesExt: [0, 2, 3, 4, 7, 9, 10],
  harm: [0, 2, 3, 5, 7, 8, 11],
};

export type PlacementLabel = keyof typeof LABEL_KEYS;

// A placement reference in a perception table: an LCM family (`{lcm, at}`, resolved via
// placementKeys) or a named scale (`{label, at}`, resolved from LABEL_KEYS). `at` is the anchor
// RELATIVE to the chord root; resolvePlacementKeys adds the actual root to place it.
export type PlacementRef = {lcm: number; at: number} | {label: PlacementLabel; at: number};

// Resolve a placement reference to folded k-tet keys, transposed by the chord's root offset. The
// LCM case reuses placementKeys (the C#-parity math); the label case rotates its base set.
export function resolvePlacementKeys(ref: PlacementRef, rootOffset: number): number[] {
  if ('lcm' in ref) return placementKeys(ref.lcm, (((ref.at + rootOffset) % 12) + 12) % 12, 12);
  return foldOctave(LABEL_KEYS[ref.label].map((k) => k + ref.at + rootOffset));
}

// A placement reference's display label at the actual (root-shifted) anchor, e.g. "8 @ 5" or
// "15s @ 7" — matching the chordWalk readout style.
function placementRefLabel(ref: PlacementRef, rootOffset: number): string {
  const at = (((ref.at + rootOffset) % 12) + 12) % 12;
  return `${'lcm' in ref ? ref.lcm : ref.label} @ ${at}`;
}

// --- Perception tables ----------------------------------------------------------------------

// One opening-key entry: the placement(s) it commits the unit to. `placements: 'any'` (the
// chord-tone openings) means "compatible with any of the chord's own placements" — resolved to a
// random one of the table's specific placements. (The perception it evokes is now implicit in the
// placement and opening key, no longer an explicit label.)
export type PerceptionEntry = {placements: PlacementRef[] | 'any'};

// A chord archetype's perception table: its 12-tet chord shape (root-relative), one entry per
// opening key 0..11 (null = no good mapping), and the stable melodic supersets used to pick the
// next chord. Transcribed from the major/minor tables in the doc.
export type PerceptionTable = {
  label: 'major' | 'minor';
  chord: number[];
  entries: (PerceptionEntry | null)[];
  stableSupersets: PlacementRef[];
};

// Major triad table (doc lines 875-878; stable supersets from lines 950-951).
const MAJOR: PerceptionTable = {
  label: 'major',
  chord: [0, 4, 7],
  entries: [
    {placements: 'any'}, // 0
    {placements: [{label: 'harm', at: 5}]}, // 1
    {placements: [{lcm: 8, at: 0}]}, // 2
    {placements: [{label: 'bluesExt', at: 0}]}, // 3
    {placements: 'any'}, // 4
    {placements: [{lcm: 8, at: 5}]}, // 5
    {placements: [{lcm: 24, at: 7}]}, // 6
    {placements: 'any'}, // 7
    {placements: [{label: '15s', at: 7}]}, // 8
    {placements: [{lcm: 24, at: 7}]}, // 9
    {placements: [{label: 'bluesExt', at: 0}]}, // 10
    {placements: [{lcm: 8, at: 0}]}, // 11
  ],
  stableSupersets: [{lcm: 24, at: 0}, {lcm: 24, at: 5}, {lcm: 24, at: 7}, {label: '15s', at: 7}],
};

// Minor triad table (doc lines 928-931; stable supersets from line 953). Opening key 4 has no
// good mapping (null).
const MINOR: PerceptionTable = {
  label: 'minor',
  chord: [0, 3, 7],
  entries: [
    {placements: 'any'}, // 0
    {placements: [{label: '15s', at: 0}, {lcm: 24, at: 8}]}, // 1
    {placements: [{lcm: 8, at: 8}]}, // 2
    {placements: 'any'}, // 3
    null, // 4
    {placements: [{lcm: 24, at: 8}]}, // 5
    {placements: [{label: 'blues', at: 3}]}, // 6
    {placements: 'any'}, // 7
    {placements: [{lcm: 8, at: 8}]}, // 8
    {placements: [{lcm: 24, at: 10}]}, // 9
    {placements: [{lcm: 24, at: 10}]}, // 10
    {placements: [{label: '15s', at: 2}, {label: 'blues', at: 8}]}, // 11
  ],
  stableSupersets: [{lcm: 24, at: 3}, {lcm: 24, at: 8}, {lcm: 24, at: 10}, {label: '15s', at: 2}],
};

// The chord vocabulary that has a hand-authored table — major and minor only, this iteration.
const TABLES: PerceptionTable[] = [MAJOR, MINOR];

// Every specific (non-'any') placement referenced by a table, deduped — the pool an 'any' entry
// draws a single placement from.
function specificPlacements(table: PerceptionTable): PlacementRef[] {
  const seen = new Set<string>();
  const out: PlacementRef[] = [];
  for (const e of table.entries) {
    if (!e || e.placements === 'any') continue;
    for (const ref of e.placements) {
      const id = 'lcm' in ref ? `l${ref.lcm}@${ref.at}` : `${ref.label}@${ref.at}`;
      if (seen.has(id)) continue;
      seen.add(id);
      out.push(ref);
    }
  }
  return out;
}

// Match a folded triad to its perception table and root offset (the transpose that maps the
// table's root-0 shape onto this chord). Null when the chord is neither major nor minor.
function identifyChord(chord: number[]): {table: PerceptionTable; root: number} | null {
  const folded = foldOctave(chord);
  const id = folded.join(',');
  for (const table of TABLES)
    for (let root = 0; root < 12; root++)
      if (foldOctave(table.chord.map((k) => k + root)).join(',') === id) return {table, root};
  return null;
}

// All 24 major/minor triads (folded), the next-chord candidate universe.
const ALL_MAJ_MIN: number[][] = (() => {
  const out: number[][] = [];
  for (let r = 0; r < 12; r++) for (const base of [[0, 4, 7], [0, 3, 7]])
    out.push(foldOctave(base.map((k) => k + r)));
  return out;
})();

// A random major/minor triad, used as the default start (or to recover from a non-maj/min start).
function randomStartChord(rng: () => number): number[] {
  const base = rng() < 0.5 ? [0, 4, 7] : [0, 3, 7];
  const r = Math.floor(rng() * 12);
  return foldOctave(base.map((k) => k + r));
}

// The next-chord candidates from a chord (identified as `table` at `root`): every major/minor
// triad that is a subset of one of the chord's stable melodic supersets. The set of legal moves
// out of the chord — shared by the open walk and the closed loop.
function nextChordCandidates(table: PerceptionTable, root: number): number[][] {
  const supersets = table.stableSupersets.map((s) => resolvePlacementKeys(s, root));
  return ALL_MAJ_MIN.filter((t) => supersets.some((sk) => isSubset(t, sk)));
}

// Pick a random non-null opening key for the chord (identified as `table` at `root`) and commit
// to one of its placements ('any' → a random specific placement of the chord), consuming two
// draws from `rng`. Returns the built step. Shared by the open walk and the closed loop so both
// articulate the opening key the same way.
function buildStep(
  chord: number[],
  table: PerceptionTable,
  root: number,
  rng: () => number,
): PerceptionStep {
  const openings = table.entries
    .map((e, idx) => ({e, idx}))
    .filter((x): x is {e: PerceptionEntry; idx: number} => x.e !== null);
  const opening = openings[Math.floor(rng() * openings.length)];
  const openingKey = (root + opening.idx) % 12;

  const refs = opening.e.placements === 'any' ? specificPlacements(table) : opening.e.placements;
  const ref = refs[Math.floor(rng() * refs.length)];
  return {
    chord,
    openingKey,
    placement: {label: placementRefLabel(ref, root), keys: resolvePlacementKeys(ref, root)},
  };
}

// Fisher–Yates shuffle a copy of `arr` using `rng`. Local to keep the perception libs decoupled.
function shuffle<T>(arr: T[], rng: () => number): T[] {
  const out = [...arr];
  for (let i = out.length - 1; i > 0; i--) {
    const j = Math.floor(rng() * (i + 1));
    [out[i], out[j]] = [out[j], out[i]];
  }
  return out;
}

// --- The walk -------------------------------------------------------------------------------

// One rhythmic unit of the progression: the chord sounding, its opening key (the pitch class the
// unit begins on, which selected the placement), and the chosen placement (label + folded melody
// keys the rest of the unit draws from).
export type PerceptionStep = {
  chord: number[];
  openingKey: number;
  placement: {label: string; keys: number[]};
};

// Generate an OPEN perception walk of length N (one step per meter group) from `start`:
//   1. Identify the current chord's table + root.
//   2. Pick a random non-null opening key; its entry gives the placement(s)
//      ('any' → a random one of the chord's specific placements).
//   3. The unit's melody draws from that placement; the opening key is the unit's first note.
//   4. Pick the next chord: a random major/minor triad that is a subset of one of the current
//      chord's stable melodic supersets.
// Returns the steps plus `next` — the chord the following pass continues from (open walk, no
// closure). `start` null ⇒ a random major/minor triad. All randomness flows through one
// mulberry32 stream, so a seed reproduces the walk (deterministic Generate re-roll).
export function generatePerceptionWalk(
  start: number[] | null,
  N: number,
  seed: number,
): {steps: PerceptionStep[]; next: number[]} {
  const rng = mulberry32(seed >>> 0);
  let current = start ? foldOctave(start) : randomStartChord(rng);
  const steps: PerceptionStep[] = [];

  for (let i = 0; i < N; i++) {
    let id = identifyChord(current);
    if (!id) {
      current = randomStartChord(rng);
      id = identifyChord(current)!;
    }
    const {table, root} = id;

    // The unit's opening key + committed placement (opening key sounded first).
    steps.push(buildStep(current, table, root, rng));

    // Next chord: a maj/min triad that is a subset of some stable superset of the current chord.
    const nextCandidates = nextChordCandidates(table, root);
    current = nextCandidates.length
      ? nextCandidates[Math.floor(rng() * nextCandidates.length)]
      : current;
  }

  return {steps, next: current};
}

// Max DFS node expansions before giving up on closing a loop (mirrors chordWalk's guard). The
// perception move graph is small (≤ 24 chords), so a legal cycle is found well within this.
const MAX_EXPANSIONS = 20000;

// Generate a CLOSED perception loop of length N (one step per meter group) that returns to its
// origin: groups 0..N-1 sound C_0..C_{N-1} (C_0 = origin) and the last group is chosen so its
// stable melodic supersets contain the origin, making the wrap C_{N-1} → origin a legal move.
// Mirrors generateChordWalk's placement-first DFS, but the move graph here is the stable-superset
// reachability between major/minor triads (opening key + placement do not gate movement, so they
// are assigned once the chord cycle is fixed). Returns the steps plus the resolved `origin` (a
// random maj/min triad when `start` is null or not maj/min) so the caller can re-loop around it.
// All randomness flows through one mulberry32 stream, so a seed reproduces the loop.
export function generatePerceptionLoop(
  start: number[] | null,
  N: number,
  seed: number,
): {steps: PerceptionStep[]; origin: number[]} {
  const rng = mulberry32(seed >>> 0);
  const startId = start ? identifyChord(foldOctave(start)) : null;
  const origin = startId ? foldOctave(start!) : randomStartChord(rng);
  const originKey = origin.join(',');

  const bake = (chords: number[][]): {steps: PerceptionStep[]; origin: number[]} => ({
    steps: chords.map((c) => {
      const {table, root} = identifyChord(c)!;
      return buildStep(c, table, root, rng);
    }),
    origin,
  });

  // N ≤ 1: a single group repeating the origin; the wrap trivially returns to origin.
  if (N <= 1) return bake([origin]);

  // Randomized DFS with backtracking over the chord move graph. path[0] = origin; for each group i
  // pick a shuffled next chord reachable from path[i]; on the last group require the origin to be
  // reachable so the wrap closes. Backtracks until a cycle closes.
  let expansions = 0;
  const path: number[][] = [origin];

  const dfs = (i: number): boolean => {
    if (++expansions > MAX_EXPANSIONS) return false;
    const {table, root} = identifyChord(path[i])!;
    const candidates = nextChordCandidates(table, root);
    if (i === N - 1) return candidates.some((c) => c.join(',') === originKey); // wrap closes?
    for (const n of shuffle(candidates, rng)) {
      path[i + 1] = n;
      if (dfs(i + 1)) return true;
    }
    return false;
  };

  // Fallback (no closing cycle): repeat the origin in every group.
  if (!dfs(0)) return bake(Array.from({length: N}, () => origin));
  return bake(path);
}
