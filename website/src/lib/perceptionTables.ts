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
// chordWalk example; `harm` is the harmonic minor scale (verified: harm rotated to anchor 5 =
// 0 1 4 5 7 8 10, the `harm@5` cited in the doc).
const LABEL_KEYS: Record<string, number[]> = {
  '15s': [0, 1, 3, 5, 9, 10],
  blues: [0, 2, 3, 4, 7, 9],
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
// next chord. Transcribed from the major/minor/dim tables in the doc.
export type PerceptionTable = {
  label: 'major' | 'minor' | 'dim';
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
    {placements: [{label: 'blues', at: 0}]}, // 3
    {placements: 'any'}, // 4
    {placements: [{lcm: 8, at: 5}]}, // 5
    {placements: [{lcm: 24, at: 7}]}, // 6
    {placements: 'any'}, // 7
    {placements: [{label: '15s', at: 7}]}, // 8
    {placements: [{lcm: 24, at: 7}]}, // 9
    {placements: [{label: 'blues', at: 0}]}, // 10
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

// Diminished triad table (doc lines 968-971; stable supersets 15s@3 and 24@1 from line 954).
// Unlike major/minor, dim has no 'any' (chord-tone) openings and no null keys: every opening key
// maps to a specific placement, and the dim feel dominates (15s@3) except where a softer 24@1 or a
// harmonic-minor colour takes over. The doc writes "15@3" in the placements row, but the stable
// melodic superset is 15s@3 (line 954; line 998 warns the lcm-15 superset must be taken in its
// stable 15s form), so it is encoded as the 15s label here.
const DIM: PerceptionTable = {
  label: 'dim',
  chord: [0, 3, 6],
  entries: [
    {placements: [{label: '15s', at: 3}]}, // 0
    {placements: [{label: '15s', at: 3}]}, // 1
    {placements: [{label: 'harm', at: 7}]}, // 2
    {placements: [{label: '15s', at: 3}]}, // 3
    {placements: [{label: '15s', at: 3}]}, // 4
    {placements: [{lcm: 24, at: 1}]}, // 5
    {placements: [{label: '15s', at: 3}]}, // 6
    {placements: [{label: 'harm', at: 7}]}, // 7
    {placements: [{lcm: 24, at: 1}]}, // 8
    {placements: [{label: 'harm', at: 7}, {label: 'harm', at: 1}]}, // 9
    {placements: [{lcm: 24, at: 1}]}, // 10
    {placements: [{label: 'harm', at: 4}]}, // 11
  ],
  stableSupersets: [{label: '15s', at: 3}, {lcm: 24, at: 1}],
};

// The chord vocabulary that has a hand-authored table — major, minor and dim.
const TABLES: PerceptionTable[] = [MAJOR, MINOR, DIM];

// Which superset list to move by when choosing the next chord:
//   'supersets' — the direct stable melodic supersets (existing behaviour).
//   'adjacency' — only the adjacency-derived lcm-24 placements (adjacencySupersets).
//   'both'      — the union of the two.
export type WalkStrategy = 'supersets' | 'adjacency' | 'both';

// Derive a table's adjacency supersets from its own 15s stable supersets via the documented rule:
// 15s@X reaches the adjacent lcm-24 placements 24@(X+1) and 24@(X+8) (doc line 1027). Single source
// of truth — no hand-typed numbers. (Verified: MAJOR 15s@7→24@8,24@3; MINOR 15s@2→24@3,24@10;
// DIM 15s@3→24@4,24@11 — matching the doc adjacency tables.)
function adjacencySupersets(table: PerceptionTable): PlacementRef[] {
  return table.stableSupersets
    .filter(
      (s): s is {label: PlacementLabel; at: number} => 'label' in s && s.label === '15s',
    )
    .flatMap((s) => [
      {lcm: 24, at: (s.at + 1) % 12},
      {lcm: 24, at: (s.at + 8) % 12},
    ]);
}

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
// table's root-0 shape onto this chord). Null when the chord is not a tabled quality (major/minor/dim).
function identifyChord(chord: number[]): {table: PerceptionTable; root: number} | null {
  const folded = foldOctave(chord);
  const id = folded.join(',');
  for (const table of TABLES)
    for (let root = 0; root < 12; root++)
      if (foldOctave(table.chord.map((k) => k + root)).join(',') === id) return {table, root};
  return null;
}

// The triad qualities that have a perception table — the base shapes of the candidate universe.
const TRIAD_SHAPES: number[][] = [[0, 4, 7], [0, 3, 7], [0, 3, 6]];

// All 36 major/minor/dim triads (folded), the next-chord candidate universe.
const ALL_TRIADS: number[][] = (() => {
  const out: number[][] = [];
  for (let r = 0; r < 12; r++) for (const base of TRIAD_SHAPES)
    out.push(foldOctave(base.map((k) => k + r)));
  return out;
})();

// A random major/minor/dim triad, used as the default start (or to recover from a non-tabled start).
function randomStartChord(rng: () => number): number[] {
  const base = TRIAD_SHAPES[Math.floor(rng() * TRIAD_SHAPES.length)];
  const r = Math.floor(rng() * 12);
  return foldOctave(base.map((k) => k + r));
}

// The next-chord candidates from a chord (identified as `table` at `root`): every major/minor/dim
// triad that is a subset of one of the chord's stable melodic supersets. The set of legal moves
// out of the chord — shared by the open walk and the closed loop.
function nextChordCandidates(
  table: PerceptionTable,
  root: number,
  strategy: WalkStrategy = 'supersets',
): number[][] {
  const refs =
    strategy === 'supersets'
      ? table.stableSupersets
      : strategy === 'adjacency'
        ? adjacencySupersets(table)
        : [...table.stableSupersets, ...adjacencySupersets(table)];
  const supersets = refs.map((s) => resolvePlacementKeys(s, root));
  return ALL_TRIADS.filter((t) => supersets.some((sk) => isSubset(t, sk)));
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

// A per-group phrase-binding constraint (from the rhythm phrase, translated to the walk). A
// superset of chordWalk's WalkBinding: it adds `splitSecondHalf` for perception mode's sub-phrase
// split. When a meter group full-repeats an earlier one the phrase can force this group to reuse
// that group's chord (`chordSource`) and/or its opening key + placement (`placementSource`); each is
// the index of an *earlier* group (< this one) or null. `splitSecondHalf` marks a half-repeat (`Ba`)
// group whose SECOND half becomes its own perception sub-unit: the walk rolls a new chord (a legal
// move out of the group's first-half chord) that the following group then continues from, and
// buildSteps rolls a fresh opening key + placement for it (attached as PerceptionStep.second). Group
// 0 (the origin) never carries a binding and is never split.
export type PerceptionBinding = {
  chordSource: number | null;
  placementSource: number | null;
  splitSecondHalf: boolean;
};

// Chord equality on folded/sorted pitch-class arrays (ALL_TRIADS / origin are always folded).
const sameChord = (a: number[], b: number[]): boolean => a.join(',') === b.join(',');

// A placement-bound step: reuse `source`'s opening key + placement for `chord`, but only when the
// source's opening key is a legal (non-null) perception opening for this chord's table/root — the
// relative index (openingKey − root) must map to a non-null entry. Null when it does not (the
// caller then falls back to a fresh buildStep). When chords are bound to the same source the
// root/table match makes this trivially valid and the placement resolves identically; when only
// placements are bound (a different chord) this is a best-effort restatement that stays within the
// perception rules (a legal opening for the new chord) or declines.
function bindStep(
  chord: number[],
  table: PerceptionTable,
  root: number,
  source: PerceptionStep,
): PerceptionStep | null {
  const rel = (((source.openingKey - root) % 12) + 12) % 12;
  if (!table.entries[rel]) return null; // not a legal opening for this chord
  return {chord, openingKey: source.openingKey, placement: source.placement};
}

// Build one PerceptionStep per chord, sequentially (so a placement-bound group can read the earlier
// step it copies). A group with `placementSource` set reuses that earlier step's opening key +
// placement when legal (bindStep); otherwise it draws a fresh opening + placement (buildStep). The
// chord axis is already resolved in the caller's chord cycle — this only honors the placement axis.
// A split (`Ba`) group additionally gets a `second` sub-step: the second-half chord from
// `secondChords[g]` gets its own fresh opening key + placement (always drawn, never bound). The RNG
// draw order is main-then-second per group, matching the DFS's chord order.
function buildSteps(
  chords: number[][],
  secondChords: (number[] | null)[],
  bindings: PerceptionBinding[] | undefined,
  rng: () => number,
): PerceptionStep[] {
  const steps: PerceptionStep[] = [];
  chords.forEach((c, g) => {
    const {table, root} = identifyChord(c)!;
    const src = bindings?.[g]?.placementSource ?? null;
    const bound = src !== null && src < g ? bindStep(c, table, root, steps[src]) : null;
    const step: PerceptionStep = bound ?? buildStep(c, table, root, rng);
    const sc = secondChords[g];
    if (sc) {
      const id = identifyChord(sc)!;
      step.second = buildStep(sc, id.table, id.root, rng);
    }
    steps.push(step);
  });
  return steps;
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
// keys the rest of the unit draws from). A half-repeat (`Ba`) group additionally carries `second` —
// the second-half sub-unit's own chord + opening key + placement — so its B part sounds a new chord
// struck mid-group and draws melody from a new placement (see PerceptionBinding.splitSecondHalf).
export type PerceptionSubStep = {
  chord: number[];
  openingKey: number;
  placement: {label: string; keys: number[]};
};
export type PerceptionStep = PerceptionSubStep & {second?: PerceptionSubStep};

// Max DFS node expansions before giving up on closing a loop / walk (mirrors chordWalk's guard).
// The perception move graph is small (≤ 36 chords), so a legal cycle is found well within this.
const MAX_EXPANSIONS = 20000;

// Generate an OPEN perception walk of length N (one step per meter group) from `start`:
//   1. Identify the current chord's table + root.
//   2. Pick a random non-null opening key; its entry gives the placement(s)
//      ('any' → a random one of the chord's specific placements).
//   3. The unit's melody draws from that placement; the opening key is the unit's first note.
//   4. Pick the next chord: a random major/minor/dim triad that is a subset of one of the current
//      chord's stable melodic supersets.
// Returns the steps plus `next` — the chord the following pass continues from (open walk, no
// closure). `start` null ⇒ a random major/minor/dim triad. All randomness flows through one
// mulberry32 stream, so a seed reproduces the walk (deterministic Generate re-roll).
//
// `bindings` (optional, one per group) lets the rhythm phrase pin a group's chord/placement to an
// earlier group's (see PerceptionBinding), mirroring chordWalk's open walk: the chord axis is
// enforced by a backtracking DFS over the move graph, the placement axis by buildSteps. Group 0
// never binds and the carry chord C_N carries no pin. A returned `unsatisfied` true means a chord
// pin could not be honored and the walk relaxed to unbound.
export function generatePerceptionWalk(
  start: number[] | null,
  N: number,
  seed: number,
  strategy: WalkStrategy = 'supersets',
  bindings?: PerceptionBinding[],
): {steps: PerceptionStep[]; next: number[]; unsatisfied: boolean} {
  // One DFS attempt at a whole walk, seeded fresh so bound/unbound attempts are independent and
  // deterministic. Returns the per-group first-half chords (`path`, length N), the parallel
  // second-half chords (`secondChords`, non-null only for split groups), the carry chord `next`
  // (the last group's ending chord), and the rng to continue baking from — or null on exhaustion. A
  // split group ends on its second-half chord, so the *next* group moves out of `endChord(i) =
  // secondChords[i] ?? path[i]`. When no group is split the second-half branch consumes no rng, so
  // the stream (and every existing seed's walk) is byte-for-byte unchanged.
  const attempt = (
    b: PerceptionBinding[] | undefined,
  ): {path: number[][]; secondChords: (number[] | null)[]; next: number[]; rng: () => number} | null => {
    const rng = mulberry32(seed >>> 0);
    const raw = start ? foldOctave(start) : randomStartChord(rng);
    const startChord = identifyChord(raw) ? raw : randomStartChord(rng);
    const path: number[][] = [startChord];
    const secondChords: (number[] | null)[] = new Array(N).fill(null);
    let expansions = 0;
    // Stay put when a chord has no stable-superset destinations (preserves the "hold current"
    // behavior instead of dead-ending).
    const movesFrom = (chord: number[]): number[][] => {
      const {table, root} = identifyChord(chord)!;
      const cands = nextChordCandidates(table, root, strategy);
      return cands.length ? cands : [chord];
    };
    const dfs = (i: number): boolean => {
      if (++expansions > MAX_EXPANSIONS) return false;
      // Roll the second-half chord first for a split group (a legal move out of path[i]); the next
      // group then continues from it. proceed() advances to group i+1 (or finishes at i === N-1).
      const proceed = (): boolean => {
        if (i === N - 1) return true; // last group assigned; carry = endChord(N-1)
        const from = secondChords[i] ?? path[i];
        const base = movesFrom(from);
        const chordPin = b?.[i + 1]?.chordSource ?? null; // group i+1 ∈ 1..N-1
        const nexts =
          chordPin !== null
            ? base.some((c) => sameChord(c, path[chordPin])) ? [path[chordPin]] : []
            : shuffle(base, rng);
        for (const n of nexts) {
          path[i + 1] = n;
          if (dfs(i + 1)) return true;
        }
        return false;
      };
      if (!b?.[i]?.splitSecondHalf) {
        secondChords[i] = null;
        return proceed();
      }
      for (const s of shuffle(movesFrom(path[i]), rng)) {
        secondChords[i] = s;
        if (proceed()) return true;
      }
      secondChords[i] = null;
      return false;
    };
    if (!dfs(0)) return null;
    return {path, secondChords, next: secondChords[N - 1] ?? path[N - 1], rng};
  };

  if (bindings) {
    const bound = attempt(bindings);
    if (bound)
      return {steps: buildSteps(bound.path, bound.secondChords, bindings, bound.rng), next: bound.next, unsatisfied: false};
  }
  const free = attempt(undefined);
  if (free)
    return {steps: buildSteps(free.path, free.secondChords, undefined, free.rng), next: free.next, unsatisfied: bindings != null};
  // Defensive: no walk at all (should not happen with stay-put) — repeat the resolved start.
  const rng = mulberry32(seed >>> 0);
  const raw = start ? foldOctave(start) : randomStartChord(rng);
  const startChord = identifyChord(raw) ? raw : randomStartChord(rng);
  return {
    steps: buildSteps(
      Array.from({length: N}, () => startChord),
      new Array(N).fill(null),
      undefined,
      rng,
    ),
    next: startChord,
    unsatisfied: bindings != null,
  };
}

// Generate a CLOSED perception loop of length N (one step per meter group) that returns to its
// origin: groups 0..N-1 sound C_0..C_{N-1} (C_0 = origin) and the last group is chosen so its
// stable melodic supersets contain the origin, making the wrap C_{N-1} → origin a legal move.
// Mirrors generateChordWalk's placement-first DFS, but the move graph here is the stable-superset
// reachability between major/minor/dim triads (opening key + placement do not gate movement, so they
// are assigned once the chord cycle is fixed). Returns the steps plus the resolved `origin` (a
// random tabled triad when `start` is null or not a tabled quality) so the caller can re-loop around it.
// All randomness flows through one mulberry32 stream, so a seed reproduces the loop.
//
// `bindings` (optional, one per group) lets the rhythm phrase pin a group's chord/placement to an
// earlier group's (see PerceptionBinding), mirroring chordWalk's closed walk: the chord axis is
// enforced during the DFS (a pinned group's chord must be a legal move), the placement axis by
// buildSteps. If the bound search finds no closing cycle the loop relaxes to an unbound walk and
// reports `unsatisfied` true — the exhaustive DFS makes that a genuine proof no bound cycle exists.
export function generatePerceptionLoop(
  start: number[] | null,
  N: number,
  seed: number,
  strategy: WalkStrategy = 'supersets',
  bindings?: PerceptionBinding[],
): {steps: PerceptionStep[]; origin: number[]; unsatisfied: boolean} {
  // The resolved origin, computed the same way in every attempt (fresh rng from the same seed) so
  // bound/unbound attempts agree on it.
  const resolveOrigin = (rng: () => number): number[] => {
    const startId = start ? identifyChord(foldOctave(start)) : null;
    return startId ? foldOctave(start!) : randomStartChord(rng);
  };

  // One DFS attempt at a closing cycle, seeded fresh. Returns the per-group first-half chords
  // (`path`, length N, path[0] = origin), the parallel second-half chords (`secondChords`, non-null
  // only for split groups), and the rng to continue baking from, or null on exhaustion. `b` toggles
  // the chord pins and the sub-phrase split. A split group ends on its second-half chord, so the
  // next group moves out of `endChord(i) = secondChords[i] ?? path[i]`, and the loop closes when
  // `endChord(N-1)` reaches the origin. With no split the second-half branch consumes no rng, so an
  // unsplit loop's stream (and every existing seed) is unchanged.
  const attempt = (
    b: PerceptionBinding[] | undefined,
  ): {origin: number[]; path: number[][]; secondChords: (number[] | null)[]; rng: () => number} | null => {
    const rng = mulberry32(seed >>> 0);
    const origin = resolveOrigin(rng);
    const originKey = origin.join(',');
    const secondChords: (number[] | null)[] = new Array(N).fill(null);

    if (N <= 1) return {origin, path: [origin], secondChords, rng};

    let expansions = 0;
    const path: number[][] = [origin];
    const movesFrom = (chord: number[]): number[][] => {
      const {table, root} = identifyChord(chord)!;
      const cands = nextChordCandidates(table, root, strategy);
      return cands.length ? cands : [chord];
    };
    const dfs = (i: number): boolean => {
      if (++expansions > MAX_EXPANSIONS) return false;
      const proceed = (): boolean => {
        const from = secondChords[i] ?? path[i];
        const {table, root} = identifyChord(from)!;
        const candidates = nextChordCandidates(table, root, strategy);
        if (i === N - 1) return candidates.some((c) => c.join(',') === originKey); // wrap closes?
        // Chord pin: force group i+1 to the pinned earlier group's chord when it is a legal move,
        // else this branch has no next and backtracks. Group i+1 ≥ 1, so path[chordSource] is set.
        const chordPin = b?.[i + 1]?.chordSource ?? null;
        const nexts =
          chordPin !== null
            ? candidates.some((c) => sameChord(c, path[chordPin])) ? [path[chordPin]] : []
            : shuffle(candidates, rng);
        for (const n of nexts) {
          path[i + 1] = n;
          if (dfs(i + 1)) return true;
        }
        return false;
      };
      if (!b?.[i]?.splitSecondHalf) {
        secondChords[i] = null;
        return proceed();
      }
      for (const s of shuffle(movesFrom(path[i]), rng)) {
        secondChords[i] = s;
        if (proceed()) return true;
      }
      secondChords[i] = null;
      return false;
    };
    return dfs(0) ? {origin, path, secondChords, rng} : null;
  };

  if (bindings) {
    const bound = attempt(bindings);
    if (bound)
      return {steps: buildSteps(bound.path, bound.secondChords, bindings, bound.rng), origin: bound.origin, unsatisfied: false};
  }
  const free = attempt(undefined);
  if (free)
    return {steps: buildSteps(free.path, free.secondChords, undefined, free.rng), origin: free.origin, unsatisfied: bindings != null};
  // No closing cycle at all: repeat the origin in every group (unbound).
  const rng = mulberry32(seed >>> 0);
  const origin = resolveOrigin(rng);
  return {
    steps: buildSteps(
      Array.from({length: N}, () => origin),
      new Array(N).fill(null),
      undefined,
      rng,
    ),
    origin,
    unsatisfied: bindings != null,
  };
}
