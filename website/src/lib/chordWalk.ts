// Chord-walk mode logic for the RhythmPatternPlayer: given an origin chord and a curated set of
// LCM-family placements, generate a *cyclic* progression of chords where every adjacent pair
// (including the wrap from the last group back to the origin) shares a placement. Each step also
// records the "bridging" placement — the one containing both the current chord and the next —
// which the player uses as that group's melody source. Pure (no React/Tone), mirroring the shape
// of placements.ts; the C# CLI stays the source of truth for the underlying placement math, which
// this reuses via placementKeys.

import {placementKeys} from './placements';
import {mulberry32, type GroupRepeat} from './rhythmPattern';

// Fold a key list into one octave of distinct pitch classes [0, 12), sorted low → high. Matches
// the same-named helper in RhythmPatternPlayerClient so patterns and chords compare consistently.
const foldOctave = (keys: number[]): number[] =>
  [...new Set(keys.map((k) => ((k % 12) + 12) % 12))].sort((a, b) => a - b);

const keyId = (keys: number[]): string => keys.join(',');

// True iff every key of `chord` is in `set` (both already folded/sorted pitch-class arrays).
const isSubset = (chord: number[], set: number[]): boolean => {
  const s = new Set(set);
  return chord.every((k) => s.has(k));
};

// --- Authored placement patterns ------------------------------------------------------------

// A curated placement *pattern* authored in MDX. Either an LCM family (resolved per-anchor via
// placementKeys, e.g. `{lcm: 8}`) or an explicit folded key-set pattern (e.g. the stable-15
// subset `{label: '15s', keys: [0,1,3,5,9,10]}`, which is NOT a plain lcm@at). expandPlacements
// rotates each pattern to all 12 anchors to build the curated pool.
export type PlacementPattern = {lcm: number} | {label: string; keys: number[]};

// A pattern's display label: its LCM (e.g. "8") or its explicit key-set label (e.g. "15s").
// Used both when expanding the pool and for the player's per-placement checkboxes, so the two
// always read identically.
export const placementPatternLabel = (p: PlacementPattern): string =>
  'lcm' in p ? String(p.lcm) : p.label;

// One curated placement instance in the expanded pool: its folded key set plus provenance (the
// source pattern's label and the anchor it was rotated to), for labels and debugging.
export type CuratedPlacement = {
  keys: number[]; // folded, sorted pitch classes 0..11
  label: string; // e.g. "8 @ 5" or "15s @ 7"
  patternLabel: string; // the source pattern's label ("8", "24", "15s")
  at: number; // rotation anchor 0..11
};

// Expand each authored pattern to all 12 anchors, deduping by key set (a pattern that self-rotates
// onto itself — e.g. a symmetric set — contributes each distinct rotation once, keeping the lowest
// anchor). `{lcm}` uses placementKeys per anchor (parity with findSupersets); `{label, keys}`
// rotates its folded base key set by `at`.
export function expandPlacements(patterns: PlacementPattern[]): CuratedPlacement[] {
  const seen = new Set<string>();
  const pool: CuratedPlacement[] = [];
  for (const pattern of patterns) {
    const isLcm = 'lcm' in pattern;
    const patternLabel = placementPatternLabel(pattern);
    const base = isLcm ? null : foldOctave(pattern.keys);
    for (let at = 0; at < 12; at++) {
      const keys = isLcm
        ? placementKeys(pattern.lcm, at, 12)
        : foldOctave(base!.map((k) => (k + at) % 12));
      const id = keyId(keys);
      if (seen.has(id)) continue;
      seen.add(id);
      pool.push({keys, label: `${patternLabel} @ ${at}`, patternLabel, at});
    }
  }
  return pool;
}

// --- Cyclic chord walk ----------------------------------------------------------------------

// A candidate chord node: its folded pitch classes plus the indices (into the curated pool) of
// every placement that contains it as a subset.
type ChordNode = {keys: number[]; placementIdxs: number[]};

// One step of the walk: the chord sounding for a meter group and the bridging placement — the
// curated placement containing both this chord and the next (C_i and C_{i+1 mod N}). The player
// draws that group's melody from `bridgingPlacement`.
export type WalkStep = {chord: number[]; bridgingPlacement: CuratedPlacement};

// A per-group phrase-binding constraint (from the rhythm phrase, translated to the walk). When a
// meter group full-repeats an earlier one, the phrase can force this group to reuse that group's
// chord and/or its bridging placement. `chordSource`/`placementSource` (if set) are the index of an
// *earlier* group (< this one) whose chord / placement this group must equal; both null ⇒ the group
// walks freely (today's behavior). Group 0 (the origin) never carries a binding.
export type WalkBinding = {chordSource: number | null; placementSource: number | null};

// Safety net: cap total DFS node expansions so a pathological graph can never hang the render.
// The graph is small (≤ a few hundred nodes, each in a handful of placements) so a valid cycle is
// found in far fewer; hitting the cap returns null and the caller degrades to origin-repeat.
const MAX_EXPANSIONS = 50_000;

// The walk's search graph: the candidate chord nodes, the inverted placement→members index, and the
// index of the start node. Shared by the closed (generateChordWalk) and open (generateOpenWalk)
// generators so the node/placement setup lives in one place.
type WalkGraph = {nodes: ChordNode[]; placementMembers: number[][]; startIdx: number};

// Build the search graph for a walk starting at `start`: every candidate chord that sits in ≥1
// placement, plus `start` itself (injected even if it is not in the heuristic vocabulary, so the
// walk can always begin there). Nodes dedup by key set. Returns null when `start` sits in no
// placement (no walk can begin). `placementMembers[p]` lists the node indices contained by pool
// placement p — the walk picks a next chord by first picking a bridging placement its current chord
// sits in, then one of that placement's members.
function buildWalkGraph(
  start: number[],
  candidates: number[][],
  pool: CuratedPlacement[],
): WalkGraph | null {
  const startKeys = foldOctave(start);

  const placementsFor = (keys: number[]): number[] => {
    const idxs: number[] = [];
    for (let p = 0; p < pool.length; p++) if (isSubset(keys, pool[p].keys)) idxs.push(p);
    return idxs;
  };

  const nodes: ChordNode[] = [];
  const nodeIdx = new Map<string, number>();
  const addNode = (keys: number[]): number => {
    const id = keyId(keys);
    const existing = nodeIdx.get(id);
    if (existing !== undefined) return existing;
    const placementIdxs = placementsFor(keys);
    const idx = nodes.length;
    nodes.push({keys, placementIdxs});
    nodeIdx.set(id, idx);
    return idx;
  };
  const startIdx = addNode(startKeys);
  if (nodes[startIdx].placementIdxs.length === 0) return null; // start sits in no placement
  for (const c of candidates) {
    const keys = foldOctave(c);
    const id = keyId(keys);
    if (nodeIdx.has(id)) continue;
    const placementIdxs = placementsFor(keys);
    if (placementIdxs.length === 0) continue;
    nodes.push({keys, placementIdxs});
    nodeIdx.set(id, nodes.length - 1);
  }

  // Inverted index: for each pool placement, the node indices it contains.
  const placementMembers: number[][] = pool.map(() => []);
  nodes.forEach((node, n) => {
    for (const p of node.placementIdxs) placementMembers[p].push(n);
  });

  return {nodes, placementMembers, startIdx};
}

// Seeded Fisher–Yates shuffle (in place) using a mulberry32 stream, so a given seed reproduces the
// same walk (deterministic Generate re-roll / loop replay).
function shuffle<T>(arr: T[], rng: () => number): T[] {
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(rng() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

// Generate a random closed walk of length N (one chord per meter group) starting and ending at
// `origin`: adjacent chords — and the wrap from the last chord back to origin — always share a
// curated placement. Returns one WalkStep per group (chord + bridging placement), or null when no
// such cycle exists (origin has no curated placement, or DFS exhausts / hits the expansion cap).
//
// `candidateChords` are the vocabulary the walk may visit (folded pitch-class arrays, supplied by
// the caller from the chosen heuristic); `pool` is the expanded curated placement pool; `seed`
// drives all randomness through a single mulberry32 stream.
//
// `bindings` (optional, one per group) lets the rhythm phrase also govern harmony: a group may pin
// its chord and/or bridging placement to an earlier group's (see WalkBinding). The DFS enforces the
// pins during search, so a returned null under active bindings is a genuine proof — via exhaustive
// backtracking — that no cycle satisfies them; the caller then relaxes to an unbound walk. Omitting
// `bindings` (or passing all-null) reproduces the free walk exactly.
export function generateChordWalk(
  origin: number[],
  candidateChords: number[][],
  pool: CuratedPlacement[],
  N: number,
  seed: number,
  bindings?: WalkBinding[],
): WalkStep[] | null {
  const originKeys = foldOctave(origin);

  const graph = buildWalkGraph(originKeys, candidateChords, pool);
  if (!graph) return null; // origin sits in no placement
  const {nodes, placementMembers, startIdx: originIdx} = graph;

  const rng = mulberry32(seed);
  const originPlacements = new Set(nodes[originIdx].placementIdxs);

  // N = 1: a single group repeating the origin. Any placement containing origin is the melody
  // source; the wrap trivially returns to origin.
  if (N <= 1) {
    const choices = shuffle([...nodes[originIdx].placementIdxs], rng);
    return [{chord: originKeys, bridgingPlacement: pool[choices[0]]}];
  }

  // Placement-first randomized DFS with backtracking. For each group i (current chord path[i]),
  // pick its bridging placement P_i FIRST — uniform-random among the placements containing the
  // current chord — then draw the next chord uniformly from that placement's members. This makes
  // the placement an unbiased free pick (a big placement holding many chords no longer gets weighted
  // by its member count, as the old flat (placement, next) edge list did). On the last group we
  // additionally require P_{N-1} to contain the origin, so the wrap chord C_N = origin closes the
  // loop; backtracking explores alternatives until a cycle closes. `chosenPlacements[i]` is the pool
  // index of P_i — the placement group i draws its melody from, bridging C_i → C_{i+1 mod N}.
  let expansions = 0;
  const path: number[] = [originIdx]; // node indices, path[0] = origin
  const chosenPlacements: number[] = []; // pool indices, chosenPlacements[i] bridges path[i] → next

  const dfs = (i: number): boolean => {
    if (++expansions > MAX_EXPANSIONS) return false;
    const cur = path[i];
    const last = i === N - 1;

    // Placement FIRST: uniform-random among placements containing cur (and, on the last group, also
    // the origin, so the wrap chord = origin closes the loop). This is the unbiased pick. A
    // placement pin instead forces P_i to reuse an earlier group's chosen placement — valid only if
    // it still contains cur (and origin, when last); otherwise this group has no placement and the
    // branch fails, forcing a backtrack.
    const placementPin = bindings?.[i]?.placementSource ?? null;
    const candidatePlacements =
      placementPin !== null
        ? (() => {
            const pinned = chosenPlacements[placementPin];
            const ok =
              nodes[cur].placementIdxs.includes(pinned) &&
              (!last || originPlacements.has(pinned));
            return ok ? [pinned] : [];
          })()
        : shuffle(
            nodes[cur].placementIdxs.filter((q) => !last || originPlacements.has(q)),
            rng,
          );

    for (const p of candidatePlacements) {
      if (last) {
        chosenPlacements[i] = p; // origin ∈ p ⇒ loop closes
        return true;
      }
      // Next chord (group i+1) drawn FROM P_i, uniform-random among its members. Adjacent chords may
      // repeat — a random draw can land on cur again, and a chord pin can force it — so a held chord
      // across two groups is a legal outcome. A chord pin forces the next chord to reuse an earlier
      // group's chord, valid whenever that chord is a member of P_i (including when it equals cur).
      const chordPin = bindings?.[i + 1]?.chordSource ?? null;
      const candidateNexts =
        chordPin !== null
          ? (() => {
              const pinned = path[chordPin];
              return placementMembers[p].includes(pinned) ? [pinned] : [];
            })()
          : shuffle([...placementMembers[p]], rng);
      for (const n of candidateNexts) {
        path[i + 1] = n;
        chosenPlacements[i] = p;
        if (dfs(i + 1)) return true; // backtrack until a loop closes
      }
    }
    return false;
  };

  if (!dfs(0)) return null;

  // One step per group: the chord sounding and the placement P_i it draws melody from.
  return path.map((node, i) => ({
    chord: nodes[node].keys,
    bridgingPlacement: pool[chosenPlacements[i]],
  }));
}

// --- Open (free-roam) chord walk ------------------------------------------------------------

// Generate an OPEN walk of length N from `start` — like generateChordWalk but with NO closure back
// to an origin. Groups 0..N-1 each sound C_i and bridge (via P_i) to C_{i+1}, visiting N+1 chords
// C_0..C_N; C_0 = start and C_N is the *carry* the following cycle starts from. Returns one WalkStep
// per group plus `next` (the carry chord), or null only when `start` sits in no placement.
//
// Free-roam mode calls this once per loop pass, feeding each pass's `next` in as the following pass's
// `start`, so the harmony wanders indefinitely rather than returning home. Params mirror
// generateChordWalk; `bindings` (one per group) pin a group's chord/placement to an earlier group's
// exactly as the closed walk does — group 0 never binds, and the unbound carry C_N carries no pin.
export function generateOpenWalk(
  start: number[],
  candidateChords: number[][],
  pool: CuratedPlacement[],
  N: number,
  seed: number,
  bindings?: WalkBinding[],
): {steps: WalkStep[]; next: number[]} | null {
  const graph = buildWalkGraph(foldOctave(start), candidateChords, pool);
  if (!graph) return null; // start sits in no placement
  const {nodes, placementMembers, startIdx} = graph;

  const rng = mulberry32(seed);
  const startKeys = nodes[startIdx].keys;

  // N = 1: a single group on `start`; pick any placement it sits in as the melody source, then draw
  // the carry chord from that placement's other members (falling back to `start` if it is a singleton).
  if (N <= 1) {
    const p = shuffle([...nodes[startIdx].placementIdxs], rng)[0];
    const others = placementMembers[p].filter((m) => m !== startIdx);
    const nextIdx = others.length ? others[Math.floor(rng() * others.length)] : startIdx;
    return {steps: [{chord: startKeys, bridgingPlacement: pool[p]}], next: nodes[nextIdx].keys};
  }

  // Placement-first randomized DFS with backtracking, mirroring generateChordWalk minus the
  // last-group origin closure: for each group i pick its bridging placement P_i (uniform among the
  // placements containing cur, or the pinned one), then draw C_{i+1} from P_i's members (which may
  // include cur — adjacent chords are allowed to repeat).
  // Backtracking only handles the rare dead-end where a pin cannot be satisfied.
  // path[0..N] are node indices (path[N] = carry); chosenPlacements[i] bridges path[i] → path[i+1].
  let expansions = 0;
  const path: number[] = [startIdx];
  const chosenPlacements: number[] = [];

  const dfs = (i: number): boolean => {
    if (i === N) return true; // groups 0..N-1 all assigned; path[N] is the carry
    if (++expansions > MAX_EXPANSIONS) return false;
    const cur = path[i];

    const placementPin = bindings?.[i]?.placementSource ?? null;
    const candidatePlacements =
      placementPin !== null
        ? (() => {
            const pinned = chosenPlacements[placementPin];
            return nodes[cur].placementIdxs.includes(pinned) ? [pinned] : [];
          })()
        : shuffle([...nodes[cur].placementIdxs], rng);

    for (const p of candidatePlacements) {
      const chordPin = bindings?.[i + 1]?.chordSource ?? null;
      const candidateNexts =
        chordPin !== null
          ? (() => {
              const pinned = path[chordPin];
              return placementMembers[p].includes(pinned) ? [pinned] : [];
            })()
          : shuffle([...placementMembers[p]], rng);
      for (const n of candidateNexts) {
        path[i + 1] = n;
        chosenPlacements[i] = p;
        if (dfs(i + 1)) return true; // backtrack until the whole open walk assigns
      }
    }
    return false;
  };

  if (!dfs(0)) return null;

  return {
    steps: chosenPlacements.map((p, i) => ({
      chord: nodes[path[i]].keys,
      bridgingPlacement: pool[p],
    })),
    next: nodes[path[N]].keys,
  };
}

// --- Phrase-bound melody ---------------------------------------------------------------------

// The three independent axes the rhythm phrase can bind in chord-walk mode: a repeated meter
// group can reuse the group it repeats's chord, bridging placement, and/or melody. The empty
// set (no axis) is "rhythm only" — the phrase repeats the rhythm alone (today's default).
export type PhraseBindAxis = 'chords' | 'placements' | 'melody';

// Parse the `phraseBinds` prop / dropdown value — a '+'-joined subset of the axes, or the
// sentinel 'rhythm' / empty string for none — into a per-axis boolean flag set. Unknown tokens
// are ignored, so old values ('chords+placements', 'placements', 'chords') still parse and
// 'rhythm' maps to all-false. Whitespace around tokens is tolerated.
export function parsePhraseBinds(s: string): Record<PhraseBindAxis, boolean> {
  const set = new Set(
    s
      .split('+')
      .map((t) => t.trim())
      .filter((t) => t.length > 0),
  );
  return {
    chords: set.has('chords'),
    placements: set.has('placements'),
    melody: set.has('melody'),
  };
}

// Bake one melody pitch class per firing event (in firing order), honoring melody phrase
// binding. Each event belongs to a meter group (its pulse index falls in that group's pulse
// range); an unbound event draws a random pitch from its group's pool, while a bound event
// copies the *actual pitch* an earlier group's aligned event played, so a repeated rhythm group
// restates that group's melodic motif note-for-note.
//
// A full-repeat group copies every event from its source; a half-repeat group copies only the
// events in its first half of pulses (the copied-rhythm region), drawing the rest fresh — a
// melody, unlike a chord, has a first half. Because the rhythm phrase already copied the source
// group's pulse velocities into the target, an aligned source event always fires (and sits at a
// lower pulse index, so its pitch is already assigned); the map lookup falls back to a fresh
// draw only defensively. When `bindMelody` is false this reproduces the free per-group draw.
//
// `firingPulseIdx` is the pulse index of each firing event, in firing order (ascending pulse
// index); `ranges` are the per-group pulse ranges (groupPulseRanges); `groupPools[g]` is group
// g's folded melody pool; `fallbackPool` covers a group with an empty pool; `repeats` is the
// parsed phrase (one per group). All randomness flows through `rng`.
export function bakeMelody(
  firingPulseIdx: number[],
  ranges: {start: number; count: number}[],
  groupPools: (number[] | null)[],
  fallbackPool: number[] | null,
  repeats: GroupRepeat[] | null,
  bindMelody: boolean,
  rng: () => number,
): number[] {
  // pulse index → firing-event ordinal, so a bound event can find its aligned source event.
  const eventOfPulse = new Map<number, number>();
  firingPulseIdx.forEach((pi, e) => eventOfPulse.set(pi, e));

  // The group a pulse index falls in (ranges are contiguous and ascending).
  const groupOfPulse = (pi: number): number => {
    for (let g = 0; g < ranges.length; g++)
      if (pi >= ranges[g].start && pi < ranges[g].start + ranges[g].count) return g;
    return ranges.length - 1; // defensive: last group
  };

  const keys: number[] = new Array(firingPulseIdx.length);
  for (let e = 0; e < firingPulseIdx.length; e++) {
    const pi = firingPulseIdx[e];
    const g = groupOfPulse(pi);
    const offset = pi - ranges[g].start;
    const rep = repeats?.[g];

    // Try to copy an earlier group's aligned event (full: whole group; half: first half only).
    if (bindMelody && rep) {
      let src: number | null = null;
      if (rep.fullSource !== null) src = rep.fullSource;
      else if (rep.halfSource !== null && offset < Math.floor(ranges[g].count / 2))
        src = rep.halfSource;
      if (src !== null) {
        // Wrap the offset to match the rhythm layer's best-fit copy: a target longer than its
        // source tiles the source, so the aligned source event lives at offset % source count
        // (identity for equal lengths and for the first-half region of a half repeat).
        const se = eventOfPulse.get(ranges[src].start + (offset % ranges[src].count));
        if (se !== undefined) {
          keys[e] = keys[se];
          continue;
        }
      }
    }

    // Unbound (or defensive fallback): fresh draw from this group's pool.
    const pool = groupPools[g] ?? fallbackPool;
    keys[e] = pool && pool.length ? pool[Math.floor(rng() * pool.length)] : 0;
  }
  return keys;
}
