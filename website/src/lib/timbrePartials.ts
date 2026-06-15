// Shared partial-spectrum math for the timbre-morph demos. Both the audio player
// (TimbreMorphPlayerClient) and the visual sweep map (PartialSweepPlot) compute the exact
// same partial frequencies and amplitudes from these functions, so the picture and the sound
// can never drift apart. See the prose in voicings-and-lcm-families.mdx (§ Dissonant LCM) and
// Marjieh et al. (2024) for what the spectra mean.

export type TimbreMorphMode =
  | 'gamma'
  | 'interval'
  | 'inharmonic'
  | 'inharmonic-sweep'
  | 'stretch-interval'
  | 'mixed-interval';

// Everything the partial formulas need that does not change as the sliders move. The two
// slider axes (t = gamma/interval/blend, s = step size) are passed per-call.
export interface TimbreConfig {
  mode: TimbreMorphMode;
  notes: number[]; // semitone offsets from the fundamental
  gamma?: number; // default 2.0 (used by 'interval')
  partials?: number; // default 10 (used by 'gamma' / 'interval' / 'inharmonic-sweep')
  rolloffDbPerOct?: number; // amplitude rolloff, default 3 dB/octave
  partialRatios?: number[]; // inharmonic target multipliers (used by 'inharmonic')
  // Per-note explicit partial multipliers (used by 'mixed-interval'): one array per note, so the
  // two voices can carry DIFFERENT fixed spectra (e.g. a harmonic lower tone {1,2,3,4} against a
  // bonang upper tone {1,1.52,3.46,3.92}). Every other mode applies one note-agnostic spectrum.
  noteRatios?: number[][];
  fundamental?: number; // Hz, default 220
}

const partials = (cfg: TimbreConfig) => cfg.partials ?? 10;

// 'inharmonic' runs exactly as many partials as the target ratio set has; 'mixed-interval' runs
// as many as the given note's explicit spectrum has (so each voice can differ); every other mode
// uses the explicit partial count, ignoring noteIndex.
export function partialCount(cfg: TimbreConfig, noteIndex = 0): number {
  if (cfg.mode === 'mixed-interval') return cfg.noteRatios![noteIndex].length;
  return cfg.mode === 'inharmonic'
    ? cfg.partialRatios?.length ?? partials(cfg)
    : partials(cfg);
}

// The notes actually sounding for the given slider values. Constant in length across the
// sliders' ranges. `t` is axis 1, `s` is axis 2 (the step size, only used by
// 'inharmonic-sweep').
export function noteOffsets(cfg: TimbreConfig, t: number, s: number): number[] {
  if (cfg.mode === 'interval') return [cfg.notes[0], t];
  if (cfg.mode === 'stretch-interval') return [cfg.notes[0], t];
  if (cfg.mode === 'mixed-interval') return [cfg.notes[0], t];
  if (cfg.mode === 'inharmonic-sweep') return [cfg.notes[0], s];
  return cfg.notes;
}

// Per-partial frequency multiplier for the given slider values. `noteIndex` only matters in
// 'mixed-interval', where each voice has its own fixed spectrum; every other mode is note-agnostic.
export function partialRatio(
  cfg: TimbreConfig,
  i: number,
  t: number,
  s: number,
  noteIndex = 0,
): number {
  // 'mixed-interval': each note carries an explicit, fixed partial set (no stretch/blend); the
  // slider t only moves the upper note's pitch (handled in noteOffsets), not its spectrum.
  if (cfg.mode === 'mixed-interval') return cfg.noteRatios![noteIndex][i];
  if (cfg.mode === 'gamma') return Math.pow(t, Math.log2(i + 1));
  // 'stretch-interval': axis 2 (s) is the stretch γ; axis 1 (t) is the interval (handled in
  // noteOffsets). Partials stretch by γ exactly as in 'gamma'.
  if (cfg.mode === 'stretch-interval') return Math.pow(s, Math.log2(i + 1));
  if (cfg.mode === 'interval')
    return Math.pow(cfg.gamma ?? 2.0, Math.log2(i + 1));
  if (cfg.mode === 'inharmonic-sweep') {
    // Blend the harmonic series (i+1) toward r^i, r = 2^(s/12): at t=1 the upper note's
    // partial i lands on the lower note's partial i+1 for any step s.
    const r = Math.pow(2, s / 12);
    return (1 - t) * (i + 1) + t * Math.pow(r, i);
  }
  // inharmonic: blend the harmonic series (i+1) toward the explicit ratios.
  const harmonic = i + 1;
  const inharmonic = cfg.partialRatios![i];
  return (1 - t) * harmonic + t * inharmonic;
}

export function noteFreq(cfg: TimbreConfig, offsetSemitones: number): number {
  return (cfg.fundamental ?? 220) * Math.pow(2, offsetSemitones / 12);
}

export function computeFreq(
  cfg: TimbreConfig,
  noteIndex: number,
  partial: number,
  t: number,
  s: number,
): number {
  return (
    noteFreq(cfg, noteOffsets(cfg, t, s)[noteIndex]) *
    partialRatio(cfg, partial, t, s, noteIndex)
  );
}

// Fixed (slider-independent) per-partial amplitude from the rolloff. Not normalised — callers
// divide by ampSum when they need partials of one note to sum to unity.
export function partialAmp(cfg: TimbreConfig, i: number): number {
  return Math.pow(10, (-(cfg.rolloffDbPerOct ?? 3) * Math.log2(i + 1)) / 20);
}

export function ampSum(cfg: TimbreConfig, noteIndex = 0): number {
  let sum = 0;
  for (let i = 0; i < partialCount(cfg, noteIndex); i++) sum += partialAmp(cfg, i);
  return sum;
}

// Interval (x-axis value) at which every upper partial coincides with a lower partial — the
// stretched-octave lock. When the upper note's frequency ratio equals the stretch γ, upper
// partial k lands on lower partial 2k for all k at once, so the whole spectrum locks. Measured
// above the lower note (cfg.notes[0]) as notes[0] + 12·log₂γ. Only defined for modes whose x-axis
// IS the interval and whose stretch is a single γ; null otherwise (e.g. 'gamma', where x IS γ).
export function lockInterval(cfg: TimbreConfig, step2: number): number | null {
  if (cfg.mode === 'stretch-interval') return cfg.notes[0] + 12 * Math.log2(step2);
  if (cfg.mode === 'interval') return cfg.notes[0] + 12 * Math.log2(cfg.gamma ?? 2);
  return null;
}
