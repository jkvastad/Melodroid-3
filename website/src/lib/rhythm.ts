import type {SequenceNote} from '@site/src/components/SequencePlayerClient';

// Named accent presets. Each pulse is one isochronous click; its accent maps to a
// (velocity, k-tet key) pair so SequencePlayer's sine renders a strong/medium/weak
// blip. Pitch (octave / fifth / root) reinforces the velocity contrast.
const ACCENTS = {
  S: {velocity: 1.0, key: 12}, // strong downbeat (octave)
  m: {velocity: 0.85, key: 7}, // medium / secondary stress (fifth)
  w: {velocity: 0.6, key: 0}, // weak (root)
  s: {velocity: 0.0, key: 0}, // silent (root)
} as const;

export type Accent = keyof typeof ACCENTS;

// Expand an explicit accent sequence into a repeating pulse stream. Each symbol is
// one pulse, exactly one grid beat apart, so the pulse rate is fixed regardless of
// accent strength or grouping. `repetitions` loops the whole sequence with onsets
// continuous across cycles; `beats` is the short blip duration.
export function pulses(
  pattern: Accent[],
  {repetitions = 4, beats = 0.5}: {repetitions?: number; beats?: number} = {},
): SequenceNote[] {
  const out: SequenceNote[] = [];
  const len = pattern.length;
  for (let r = 0; r < repetitions; r++) {
    pattern.forEach((sym, i) => {
      const {velocity, key} = ACCENTS[sym];
      out.push({
        key,
        beat: r * len + i, // continuous across cycles
        beats, // short blip
        velocity,
      });
    });
  }
  return out;
}

const gcd = (x: number, y: number): number => (y === 0 ? x : gcd(y, x % y));
const lcm = (x: number, y: number): number => (x * y) / gcd(x, y);

// Build a two-voice polyrhythm: `a` against `b` isochronous onsets sharing one
// cycle. The cycle is the LCM(a, b) tatum grid; a voice with count n places its
// onsets at i*grid/n (fractional beats are fine — the player schedules each note
// by absolute beat). The two voices sit on distinct pitches (`keyA`/`keyB`) so the
// streams are tellable apart by ear, with the coincident downbeat (beat 0)
// accented full and the off-onsets lighter. `cycles` loops continuously.
export function polyrhythm(
  a: number,
  b: number,
  {
    cycles = 4,
    keyA = 12,
    keyB = 7,
    beats = 0.5,
  }: {cycles?: number; keyA?: number; keyB?: number; beats?: number} = {},
): SequenceNote[] {
  const grid = lcm(a, b);
  const out: SequenceNote[] = [];
  for (let c = 0; c < cycles; c++) {
    const base = c * grid;
    const voice = (count: number, key: number) => {
      for (let i = 0; i < count; i++) {
        out.push({
          key,
          beat: base + (i * grid) / count, // continuous across cycles
          beats, // short blip
          velocity: i === 0 ? 1.0 : 0.7, // shared downbeat accented
        });
      }
    };
    voice(a, keyA);
    voice(b, keyB);
  }
  return out;
}

// Per-stream beat tempo for a polyrhythm at a given tatum length. A stream of
// `count` onsets over the LCM(streams) tatum grid sounds a beat every grid/count
// tatums; bpm = 60 / (beat period in seconds). Drives the tempo-slider caption so
// the reader sees each candidate beat's rate (e.g. 2-beat: 100 BPM · 3-beat: 150 BPM).
export function streamTempos(
  streams: number[],
  beatSec: number,
): {count: number; bpm: number}[] {
  const grid = streams.reduce((g, n) => lcm(g, n), 1);
  return streams.map((count) => ({
    count,
    bpm: Math.round(60 / ((grid / count) * beatSec)),
  }));
}
