import type {SequenceNote} from '@site/src/components/SequencePlayerClient';

// Build a repeating stream of eighth-note pulses from an additive grouping.
// First pulse of the whole pattern = downbeat (strong); first pulse of each later
// group = secondary stress; everything else = weak. Accents are encoded as
// pitch (k-tet key) + velocity so SequencePlayer's sine renders strong/weak clicks.
export function pulsePattern(
  groups: number[],
  {cycles = 4, beats = 0.5}: {cycles?: number; beats?: number} = {},
): SequenceNote[] {
  const len = groups.reduce((a, b) => a + b, 0);
  const heads = new Set<number>();
  let p = 0;
  for (const g of groups) {
    heads.add(p);
    p += g;
  }

  const out: SequenceNote[] = [];
  for (let c = 0; c < cycles; c++) {
    for (let i = 0; i < len; i++) {
      const downbeat = i === 0;
      const head = heads.has(i);
      out.push({
        key: downbeat ? 12 : head ? 7 : 0, // octave / fifth / root
        beat: c * len + i, // continuous across cycles
        beats, // short blip
        // Tightened range: a clearly audible weak floor so the accent
        // contrast reads as stress, not near-silence.
        velocity: downbeat ? 1 : head ? 0.85 : 0.6,
      });
    }
  }
  return out;
}
