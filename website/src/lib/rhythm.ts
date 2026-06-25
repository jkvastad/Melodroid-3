import type {SequenceNote} from '@site/src/components/SequencePlayerClient';

// Named accent presets. Each pulse is one isochronous click; its accent maps to a
// (velocity, k-tet key) pair so SequencePlayer's sine renders a strong/medium/weak
// blip. Pitch (octave / fifth / root) reinforces the velocity contrast.
const ACCENTS = {
  S: {velocity: 1.0, key: 12}, // strong downbeat (octave)
  m: {velocity: 0.85, key: 7}, // medium / secondary stress (fifth)
  w: {velocity: 0.6, key: 0}, // weak (root)
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
