// Pure rhythm-pattern generation: turn a meter + subdivision grid into a set of
// velocity-bearing pulses, shaped by two knobs — resolution (how many pulses fire)
// and syncopation (how far the accents drift off the meter). No I/O, no audio; the
// RhythmPatternPlayer client renders and sounds the Pulse[] this produces. Sibling
// to rhythm.ts.

// ---- Parsing -------------------------------------------------------------

// Split on whitespace and/or commas, dropping empties: "7, 2 3" → ["7","2","3"].
function tokenize(text: string): string[] {
  return text.split(/[\s,]+/).filter((t) => t.length > 0);
}

function parsePositiveInts(tokens: string[], noun: string): number[] {
  return tokens.map((t) => {
    if (!/^\d+$/.test(t)) throw new Error(`"${t}" is not a whole number.`);
    const n = parseInt(t, 10);
    if (n < 1) throw new Error(`${noun} must be at least 1.`);
    return n;
  });
}

// Parse "7 2 3" / "7,2,3" → [7,2,3]. Each meter group is the length (in unit beats)
// of one accent group; its first beat carries the heavy meter accent. Throws on
// malformed input so the client can show an inline hint and keep the last valid
// meter.
export function parseMeter(text: string): number[] {
  const tokens = tokenize(text);
  if (tokens.length === 0)
    throw new Error('Enter at least one group, e.g. "4" or "7 2 3".');
  return parsePositiveInts(tokens, 'Meter groups');
}

// Parse the subdivision spec against a known meter length. A single number applies
// to every group; otherwise there must be exactly one number per meter group (a
// different fastest pulse per beat of the meter). Returns one subdivision per group.
export function parseSubdivisions(text: string, meterLen: number): number[] {
  const tokens = tokenize(text);
  if (tokens.length === 0) throw new Error('Enter a subdivision, e.g. "2".');
  const nums = parsePositiveInts(tokens, 'Subdivisions');
  if (nums.length === 1) return Array<number>(meterLen).fill(nums[0]);
  if (nums.length !== meterLen)
    throw new Error(
      `Give 1 subdivision or ${meterLen} (one per meter group), got ${nums.length}.`,
    );
  return nums;
}

// ---- Phrase repetition ---------------------------------------------------

// A per-group repetition instruction. `fullSource` (if set) is the index of an earlier,
// structurally identical group whose whole rhythm this group copies; `halfSource` (if set)
// is the index of an earlier group whose *first half* this group's first half copies (its
// second half stays freshly generated). Both null ⇒ the group generates fresh (today's
// behavior). A group carries at most one of the two.
export type GroupRepeat = {fullSource: number | null; halfSource: number | null};

// Each group's slice into the flat pulse array: its start index and pulse count
// (count = meter[g] * subdivisions[g]). The flat-array analogue of the group bookkeeping
// in buildGrid / gridLines.
export function groupPulseRanges(
  meter: number[],
  subdivisions: number[],
): {start: number; count: number}[] {
  const ranges: {start: number; count: number}[] = [];
  let start = 0;
  meter.forEach((m, g) => {
    const count = m * subdivisions[g];
    ranges.push({start, count});
    start += count;
  });
  return ranges;
}

// Parse a phrase string into one GroupRepeat per meter group. Each token is an uppercase
// identity letter with an optional trailing lowercase letter: a reused uppercase letter marks
// a full copy of the first group with that identity; a trailing lowercase `x` marks a first-half
// copy of group `X`. Whitespace and commas between tokens are ignored, so "ABaACa" and
// "A Ba A Ca" parse identically. Empty text yields all-null repeats (no repetition). Throws a
// friendly Error on malformed input, a mismatched token count, a forward/self reference, or a
// structurally incompatible source, so the client can show an inline hint.
export function parsePhrase(
  text: string,
  meter: number[],
  subdivisions: number[],
): GroupRepeat[] {
  const none = (): GroupRepeat[] =>
    meter.map(() => ({fullSource: null, halfSource: null}));
  if (text.trim().length === 0) return none();

  // Scan into tokens: each uppercase starts a token, an optional following lowercase attaches.
  const tokens: {upper: string; lower: string | null}[] = [];
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (/\s|,/.test(ch)) continue;
    if (!/[A-Z]/.test(ch))
      throw new Error(
        `"${ch}" is not a group letter — use A–Z with an optional lowercase, e.g. "ABAC".`,
      );
    let lower: string | null = null;
    if (i + 1 < text.length && /[a-z]/.test(text[i + 1])) {
      lower = text[++i];
    }
    tokens.push({upper: ch, lower});
  }

  if (tokens.length !== meter.length)
    throw new Error(
      `Give one group letter per meter group (${meter.length}), got ${tokens.length}.`,
    );

  // First index carrying each uppercase identity, for resolving copies.
  const firstOf = new Map<string, number>();
  tokens.forEach((t, g) => {
    if (!firstOf.has(t.upper)) firstOf.set(t.upper, g);
  });

  // Structural identity: same length and same subdivision ⇒ pulse-for-pulse copyable.
  const compatible = (a: number, b: number): boolean =>
    meter[a] === meter[b] && subdivisions[a] === subdivisions[b];

  return tokens.map((t, g) => {
    const isFirst = firstOf.get(t.upper) === g;
    const fullSource = isFirst ? null : firstOf.get(t.upper)!;
    let halfSource: number | null = null;

    if (t.lower) {
      if (fullSource !== null)
        throw new Error(
          `Group ${g + 1} (${t.upper}${t.lower}) can't be both a full repeat of "${t.upper}" and a half repeat.`,
        );
      const upperOfLower = t.lower.toUpperCase();
      const src = firstOf.get(upperOfLower);
      if (src === undefined || src >= g)
        throw new Error(
          `Group ${g + 1} (${t.upper}${t.lower}) half-repeats "${upperOfLower}", which must appear in an earlier group.`,
        );
      halfSource = src;
    }

    if (fullSource !== null && !compatible(fullSource, g))
      throw new Error(
        `Group ${g + 1} (${t.upper}) can't copy group ${fullSource + 1}: different length or subdivision.`,
      );
    if (halfSource !== null) {
      if (!compatible(halfSource, g))
        throw new Error(
          `Group ${g + 1} (${t.upper}${t.lower}) can't half-copy group ${halfSource + 1}: different length or subdivision.`,
        );
      if (meter[g] % 2 !== 0)
        throw new Error(
          `Group ${g + 1} (${t.upper}${t.lower}) needs an even length to half-repeat, got ${meter[g]}.`,
        );
    }

    return {fullSource, halfSource};
  });
}

// ---- Pulse model ---------------------------------------------------------

export type Pulse = {
  unitBeat: number; // onset in unit beats (fractional for subdivision pulses)
  velocity: number; // MIDI velocity, integer [0,100]; 0 = silent / inactive
};

export type PatternParams = {
  meter: number[];
  subdivisions: number[]; // one per meter group (parseSubdivisions guarantees this)
  syncopation: number; // [0,1] — deviation from meter
  resolution: number; // [0,1] — fraction of the grid that fires
  repeats?: GroupRepeat[]; // optional phrase repetition, one per group (parsePhrase output)
};

// Internal metric-strength tiers (never emitted). Only the ordering and rough
// spacing matter: generation reads these to decide activation and accent, then
// collapses them into a single MIDI velocity per pulse.
const HEAVY = 1.0; // group-start beat — the heavy meter accent
const BEAT = 0.72; // on-beat that is not a group start
const SUB_FALLOFF = 0.62; // each subdivision depth is this much weaker than a beat

// The heaviest accent maps to this MIDI velocity rather than the full 127 — a velocity
// of 127 is extreme in common musical use, so we leave headroom and let every lighter
// tier scale proportionally beneath it. The plot keeps a 0–127 axis and draws these
// true velocities, so the heaviest accent reads at 96 with the rest of the MIDI range
// left as visible headroom.
const MAX_VELOCITY = 96;
const MIN_ACTIVE_VELOCITY = 19; // audibility floor so a firing pulse stays audible/visible
const DEMOTE_PROB = 0.7; // ceiling on how often a heavy beat is knocked down at y=1

const lerp = (a: number, b: number, t: number): number => a + (b - a) * t;
const clamp01 = (x: number): number => Math.max(0, Math.min(1, x));

// Smallest prime factor, used to peel a subdivision apart one metric level at a time.
function smallestPrimeFactor(n: number): number {
  for (let p = 2; p * p <= n; p++) if (n % p === 0) return p;
  return n; // n is prime (or 1)
}

// Metric depth of the i-th pulse within a beat split into `sub` equal parts. The
// beat first divides into its smallest prime factor p (those p−1 boundaries are
// depth 1), then each segment recurses — so an even split's centre pulse is stronger
// than the pulses flanking it (sub=4 → depths [0,2,1,2]; sub=3 → [0,1,1]). Deeper =
// weaker. Only called for i ≥ 1, which implies sub ≥ 2.
function subdivisionDepth(i: number, sub: number): number {
  if (i === 0) return 0;
  const p = smallestPrimeFactor(sub);
  const q = sub / p;
  if (i % q === 0) return 1; // lands on a first-level boundary
  return 1 + subdivisionDepth(i % q, q);
}

type GridPulse = {
  unitBeat: number;
  strength: number; // internal metric strength (0, 1]
  isOnBeat: boolean; // j === 0 — a unit beat, always eligible to fire
  isGroupStart: boolean; // the heavy meter accent (first beat of a group)
};

// Lay out every pulse of one cycle with its internal metric strength. Unit beats sit
// at integer positions; each beat of group g splits into `subdivisions[g]` pulses.
function buildGrid(meter: number[], subdivisions: number[]): GridPulse[] {
  const grid: GridPulse[] = [];
  let beatIndex = 0; // absolute unit-beat index across the whole cycle
  for (let g = 0; g < meter.length; g++) {
    const sub = subdivisions[g];
    for (let bWithin = 0; bWithin < meter[g]; bWithin++) {
      for (let j = 0; j < sub; j++) {
        const isOnBeat = j === 0;
        const isGroupStart = bWithin === 0 && j === 0;
        const strength = isOnBeat
          ? isGroupStart
            ? HEAVY
            : BEAT
          : BEAT * Math.pow(SUB_FALLOFF, subdivisionDepth(j, sub));
        grid.push({
          unitBeat: beatIndex + j / sub,
          strength,
          isOnBeat,
          isGroupStart,
        });
      }
      beatIndex++;
    }
  }
  return grid;
}

// Per-pulse metric strength tiers (the internal skeleton driving generation). Exposed
// mainly for tests / inspection; the emitted Pulse[] carries only velocity.
export function metricStrength(
  meter: number[],
  subdivisions: number[],
): number[] {
  return buildGrid(meter, subdivisions).map((p) => p.strength);
}

// Small, fast seeded PRNG (Tommy Ettinger's mulberry32). Kept here so generation is
// pure and reproducible: a given seed always yields the same pattern, and Generate
// just advances the seed. Exported so the player can pitch a pattern deterministically
// (the same seed → same melody) from this one source of randomness.
export function mulberry32(seed: number): () => number {
  let a = seed >>> 0;
  return function () {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function toVelocity(strength01: number): number {
  const v = Math.round(strength01 * MAX_VELOCITY);
  return Math.max(MIN_ACTIVE_VELOCITY, Math.min(MAX_VELOCITY, v));
}

// Realize a concrete pattern from the parameters and a seed.
//
// Resolution gates activation: unit beats always fire, while each subdivision pulse
// is a Bernoulli draw whose probability rises with r and with the pulse's metric
// strength (stronger subdivisions — an even split's centre — fill in before the
// weak ones). r=0 leaves only the unit beats; r=1 fires everything.
//
// Syncopation then *redistributes* accent rather than only adding it: a firing
// off-beat pulse may be promoted toward a heavy accent, and a heavy meter beat may be
// demoted to a lighter tier, both with probability rising in y. At y=0 velocity
// strictly tracks metric strength (classic on-meter accents); as y rises the strong
// accents drift off the meter beats.
export function generatePattern(
  params: PatternParams,
  seed: number,
): {pulses: Pulse[]; totalBeats: number} {
  const {meter, subdivisions, syncopation, resolution, repeats} = params;
  const grid = buildGrid(meter, subdivisions);
  const totalBeats = meter.reduce((a, b) => a + b, 0);
  const rng = mulberry32(seed);

  const pulses: Pulse[] = grid.map((p) => {
    // 1. Activation (resolution gate).
    if (!p.isOnBeat) {
      // Weaker subdivisions need a higher exponent, so they only switch on as r → 1.
      const pActive = Math.pow(resolution, BEAT / p.strength);
      if (rng() >= pActive) return {unitBeat: p.unitBeat, velocity: 0};
    }

    // 2. Base velocity tracks metric strength.
    let v = p.strength;

    // 3. Syncopation redistributes accent.
    if (p.isGroupStart) {
      // Heavy meter pulse: knock it down to a lighter tier.
      if (rng() < syncopation * DEMOTE_PROB) {
        v = lerp(BEAT * 0.45, BEAT, rng()); // land somewhere in the beat/sub range
      }
    } else {
      // Off-beat / non-strong pulse: promote toward a heavy accent. Weaker pulses are
      // promoted more readily, so the accents that appear are the off-beat ones.
      const pPromote = syncopation * clamp01(1 - p.strength);
      if (rng() < pPromote) {
        v = lerp(v, 1, 0.6 + 0.4 * rng()); // toward heavy
      }
    }

    return {unitBeat: p.unitBeat, velocity: toVelocity(v)};
  });

  // Phrase repetition: overwrite target groups' velocities by copying from an earlier group,
  // after the base pass so the seeded RNG stream (and thus every seed) is unchanged. A full
  // copy replays the whole source group; a half copy replays only its first half, leaving the
  // second half as freshly generated. Only velocity is copied — each pulse keeps its unitBeat.
  if (repeats) {
    const ranges = groupPulseRanges(meter, subdivisions);
    repeats.forEach((rep, g) => {
      const {start, count} = ranges[g];
      if (rep.fullSource !== null) {
        const src = ranges[rep.fullSource];
        for (let k = 0; k < count; k++)
          pulses[start + k].velocity = pulses[src.start + k].velocity;
      } else if (rep.halfSource !== null) {
        const src = ranges[rep.halfSource];
        const half = Math.floor(count / 2);
        for (let k = 0; k < half; k++)
          pulses[start + k].velocity = pulses[src.start + k].velocity;
      }
    });
  }

  return {pulses, totalBeats};
}

// Vertical grid-line positions for the visualization (in unit beats): bold at
// group starts (0…totalBeats, bracketing the cycle), medium at every unit beat, faint
// at every pulse. Derived from the same layout as buildGrid so the lines register
// exactly with the bars.
export function gridLines(
  meter: number[],
  subdivisions: number[],
): {groupStarts: number[]; unitBeats: number[]; pulses: number[]} {
  const totalBeats = meter.reduce((a, b) => a + b, 0);
  const groupStarts: number[] = [];
  let acc = 0;
  for (const m of meter) {
    groupStarts.push(acc);
    acc += m;
  }
  groupStarts.push(totalBeats); // closing bracket = next cycle's downbeat

  const unitBeats: number[] = [];
  for (let b = 0; b <= totalBeats; b++) unitBeats.push(b);

  const pulses: number[] = [];
  let beatIndex = 0;
  meter.forEach((m, g) => {
    const sub = subdivisions[g];
    for (let bWithin = 0; bWithin < m; bWithin++) {
      for (let j = 0; j < sub; j++) pulses.push(beatIndex + j / sub);
      beatIndex++;
    }
  });

  return {groupStarts, unitBeats, pulses};
}
