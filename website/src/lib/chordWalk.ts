// Chord-walk mode logic for the RhythmPatternPlayer: given an origin chord and a curated set of
// LCM-family placements, generate a *cyclic* progression of chords where every adjacent pair
// (including the wrap from the last group back to the origin) shares a placement. Each step also
// records the "bridging" placement — the one containing both the current chord and the next —
// which the player uses as that group's melody source. Pure (no React/Tone), mirroring the shape
// of placements.ts; the C# CLI stays the source of truth for the underlying placement math, which
// this reuses via placementKeys.

import {placementKeys} from './placements';
import {mulberry32} from './rhythmPattern';

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
    const patternLabel = isLcm ? String(pattern.lcm) : pattern.label;
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

// Safety net: cap total DFS node expansions so a pathological graph can never hang the render.
// The graph is small (≤ a few hundred nodes, each in a handful of placements) so a valid cycle is
// found in far fewer; hitting the cap returns null and the caller degrades to origin-repeat.
const MAX_EXPANSIONS = 50_000;

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
export function generateChordWalk(
  origin: number[],
  candidateChords: number[][],
  pool: CuratedPlacement[],
  N: number,
  seed: number,
): WalkStep[] | null {
  const originKeys = foldOctave(origin);

  const placementsFor = (keys: number[]): number[] => {
    const idxs: number[] = [];
    for (let p = 0; p < pool.length; p++) if (isSubset(keys, pool[p].keys)) idxs.push(p);
    return idxs;
  };

  // Build the node set: every candidate chord that sits in ≥1 placement, plus the origin (injected
  // even if it is not in the heuristic vocabulary, so the walk can always start there). Dedup by
  // key set. nodeIdx maps a chord id → its node index.
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
  const originIdx = addNode(originKeys);
  if (nodes[originIdx].placementIdxs.length === 0) return null; // origin sits in no placement
  for (const c of candidateChords) {
    const keys = foldOctave(c);
    const id = keyId(keys);
    if (nodeIdx.has(id)) continue;
    const placementIdxs = placementsFor(keys);
    if (placementIdxs.length === 0) continue;
    nodes.push({keys, placementIdxs});
    nodeIdx.set(id, nodes.length - 1);
  }

  // Inverted index: for each pool placement, the node indices it contains. The walk picks a next
  // chord by first picking a bridging placement the current chord sits in, then one of its members.
  const placementMembers: number[][] = pool.map(() => []);
  nodes.forEach((node, n) => {
    for (const p of node.placementIdxs) placementMembers[p].push(n);
  });

  const rng = mulberry32(seed);
  const originPlacements = new Set(nodes[originIdx].placementIdxs);

  // N = 1: a single group repeating the origin. Any placement containing origin is the melody
  // source; the wrap trivially returns to origin.
  if (N <= 1) {
    const choices = shuffle([...nodes[originIdx].placementIdxs], rng);
    return [{chord: originKeys, bridgingPlacement: pool[choices[0]]}];
  }

  // The successor candidates from `cur`: each (bridging placement p, next node n) pair where p
  // contains cur and n ≠ cur. `requireWrap` (used only for the last chord) additionally requires n
  // to share a placement with origin, so the wrap edge back to origin is guaranteed to exist.
  type Edge = {placement: number; next: number};
  const successors = (cur: number, requireWrap: boolean): Edge[] => {
    const edges: Edge[] = [];
    for (const p of nodes[cur].placementIdxs)
      for (const n of placementMembers[p]) {
        if (n === cur) continue;
        if (requireWrap && !nodes[n].placementIdxs.some((q) => originPlacements.has(q))) continue;
        edges.push({placement: p, next: n});
      }
    return edges;
  };

  // A pool placement shared by node `n` and origin, chosen with the seeded stream (the wrap
  // bridging placement P_{N-1}).
  const wrapPlacement = (n: number): CuratedPlacement => {
    const shared = shuffle(
      nodes[n].placementIdxs.filter((q) => originPlacements.has(q)),
      rng,
    );
    return pool[shared[0]];
  };

  // Randomized DFS over positions 1..N-1. `bridges[i]` bridges chord i → chord i+1; on success the
  // caller reads bridges[0..N-2] and adds the wrap bridge for the last chord. depth counts chords
  // chosen after origin (1-based); at depth N-1 we are choosing the last chord and require the wrap.
  let expansions = 0;
  const path: number[] = [originIdx]; // node indices, path[0] = origin
  const bridges: number[] = []; // pool indices, bridges[i] bridges path[i] → path[i+1]

  const dfs = (depth: number): boolean => {
    if (++expansions > MAX_EXPANSIONS) return false;
    const cur = path[path.length - 1];
    const last = depth === N - 1;
    for (const edge of shuffle(successors(cur, last), rng)) {
      path.push(edge.next);
      bridges.push(edge.placement);
      if (last) return true; // last chord chosen with a valid wrap; done
      if (dfs(depth + 1)) return true;
      path.pop();
      bridges.pop();
    }
    return false;
  };

  if (!dfs(1)) return null;

  // Assemble N steps. bridges[i] bridges chord i → i+1 for i in 0..N-2; the wrap bridge for the
  // last chord (→ origin) is a placement shared by the last chord and origin.
  const steps: WalkStep[] = [];
  for (let i = 0; i < N; i++) {
    const bridge = i < N - 1 ? pool[bridges[i]] : wrapPlacement(path[i]);
    steps.push({chord: nodes[path[i]].keys, bridgingPlacement: bridge});
  }
  return steps;
}
