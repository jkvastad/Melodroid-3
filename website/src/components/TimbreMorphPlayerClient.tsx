import React, {useEffect, useRef, useState} from 'react';
import * as Tone from 'tone';

export type TimbreMorphMode = 'gamma' | 'interval' | 'inharmonic';

export type TimbreMorphPlayerProps = {
  // Which axis the slider drives:
  //   'gamma'      — slider IS the stretch factor γ; notes are fixed. Partial i of a
  //                  note at offset s sits at f0·2^(s/12)·γ^log2(i+1).
  //   'interval'   — slider IS the UPPER voice's semitone offset; the lower voice is
  //                  fixed at notes[0] and the timbre (γ) is fixed. notes[1+] are
  //                  ignored — the upper voice comes from the slider (starting at
  //                  `initial`).
  //   'inharmonic' — slider IS a blend t∈[0,1] from the harmonic partials (i+1) to the
  //                  explicit `partialRatios` set; notes and interval are fixed.
  mode: TimbreMorphMode;
  notes: number[]; // semitone offsets from the fundamental
  min: number;
  max: number;
  initial: number; // starting slider value
  step?: number; // slider granularity; default 0.01
  // Fixed-spectrum parameters (see mode notes above for which apply).
  gamma?: number; // default 2.0 (used by 'interval')
  partials?: number; // default 10 (used by 'gamma' / 'interval')
  rolloffDbPerOct?: number; // amplitude rolloff, default 3 dB/octave
  partialRatios?: number[]; // inharmonic target multipliers (used by 'inharmonic')
  fundamental?: number; // Hz, default 220
  gain?: number; // linear amplitude multiplier; default 0.5
  label?: string; // button label; default 'Play'
  readout?: (m: number) => string; // formats the slider value into a caption
};

// A slider-driven additive-synth player: it holds a sustained chord and re-ramps each
// partial's frequency in realtime as the slider moves, so a fixed interval can be heard
// going consonant -> rough (and back) continuously under the reader's own control. This
// is the interactive companion to the static TimbrePlayer it replaces; the spectrum is
// built from explicit partials because Tone.Synth is harmonic-only (Marjieh et al. 2024).
//
// Lifecycle mirrors the other players: build lazily on a user gesture, track every node,
// and dispose (not just release) on stop/unmount — disposing is the only way to silence
// the free-running oscillators.
export default function TimbreMorphPlayerClient({
  mode,
  notes,
  min,
  max,
  initial,
  step = 0.01,
  gamma = 2.0,
  partials = 10,
  rolloffDbPerOct = 3,
  partialRatios,
  fundamental = 220,
  gain = 0.5,
  label = 'Play',
  readout,
}: TimbreMorphPlayerProps) {
  // Master gain the whole signal runs through, so stop() can fade to zero (no click)
  // before disposing.
  const masterRef = useRef<Tone.Gain | null>(null);
  const nodesRef = useRef<Array<{dispose: () => void}>>([]);
  // The oscillators tagged with which note / partial they realise, so the slider
  // handler can recompute and re-ramp each one's frequency.
  const partialsRef = useRef<
    Array<{osc: Tone.Oscillator; noteIndex: number; partial: number}>
  >([]);
  const [slider, setSlider] = useState(initial);
  const [playing, setPlaying] = useState(false);

  // 'inharmonic' must run exactly as many partials as the target ratio set has.
  const partialCount =
    mode === 'inharmonic' ? partialRatios?.length ?? partials : partials;

  // The notes actually sounding for a given slider value. Constant in length across the
  // slider's range (so the oscillator set never changes — we only re-ramp frequencies).
  const noteOffsets = (m: number): number[] =>
    mode === 'interval' ? [notes[0], m] : notes;

  // Per-partial frequency multiplier for a given slider value.
  const partialRatio = (i: number, m: number): number => {
    if (mode === 'gamma') return Math.pow(m, Math.log2(i + 1));
    if (mode === 'interval') return Math.pow(gamma, Math.log2(i + 1));
    // inharmonic: blend the harmonic series (i+1) toward the explicit ratios.
    const harmonic = i + 1;
    const inharmonic = partialRatios![i];
    return (1 - m) * harmonic + m * inharmonic;
  };

  const noteFreq = (offsetSemitones: number) =>
    fundamental * Math.pow(2, offsetSemitones / 12);

  const computeFreq = (noteIndex: number, partial: number, m: number) =>
    noteFreq(noteOffsets(m)[noteIndex]) * partialRatio(partial, m);

  // Fixed (slider-independent) per-partial amplitude from the rolloff, normalised so the
  // partials of one note sum to unity before the per-note headroom split.
  const partialAmp = (i: number) =>
    Math.pow(10, (-rolloffDbPerOct * Math.log2(i + 1)) / 20);
  const ampSum = Array.from({length: partialCount}, (_, i) => partialAmp(i)).reduce(
    (a, b) => a + b,
    0,
  );

  const defaultReadout = (m: number): string => {
    if (mode === 'gamma') return `γ = ${m.toFixed(2)}`;
    if (mode === 'interval') return `${m.toFixed(2)} st`;
    return `harmonic ↔ inharmonic: ${Math.round(m * 100)}%`;
  };
  const caption = (readout ?? defaultReadout)(slider);

  const stop = () => {
    const master = masterRef.current;
    const nodes = nodesRef.current;
    masterRef.current = null;
    nodesRef.current = [];
    partialsRef.current = [];
    setPlaying(false);
    if (!master) return;
    const fadeSec = 0.1;
    master.gain.rampTo(0, fadeSec); // fade sounding partials, no click
    setTimeout(() => {
      nodes.forEach((n) => n.dispose()); // disposing silences the free-running oscillators
      master.dispose();
    }, fadeSec * 1000 + 50);
  };

  useEffect(() => () => stop(), []); // dispose on unmount

  const play = async () => {
    await Tone.start(); // unlock audio on the user gesture
    const t0 = Tone.now() + 0.05; // small lead-in so the attack isn't clipped
    const atk = 0.02;
    // Master starts silent and ramps up — a soft attack with no click.
    const master = new Tone.Gain(0).toDestination();
    master.gain.setValueAtTime(0, t0);
    master.gain.linearRampToValueAtTime(1, t0 + atk);
    masterRef.current = master;

    const nodes: Array<{dispose: () => void}> = [];
    const tagged: Array<{osc: Tone.Oscillator; noteIndex: number; partial: number}> = [];

    const offsets = noteOffsets(slider);
    // Split headroom across the simultaneous notes so a chord doesn't clip.
    const noteScale = gain / Math.max(offsets.length, 1);
    offsets.forEach((_, noteIndex) => {
      for (let i = 0; i < partialCount; i++) {
        const pGain = new Tone.Gain((partialAmp(i) / ampSum) * noteScale).connect(
          master,
        );
        const osc = new Tone.Oscillator(
          computeFreq(noteIndex, i, slider),
          'sine',
        ).connect(pGain);
        osc.start(t0);
        nodes.push(osc, pGain);
        tagged.push({osc, noteIndex, partial: i});
      }
    });

    nodesRef.current = nodes;
    partialsRef.current = tagged;
    setPlaying(true);
  };

  // Update the slider value; if sounding, glide every partial to its new frequency. A
  // short ramp (40 ms) kills zipper noise while staying responsive to dragging.
  const onSlide = (value: number) => {
    setSlider(value);
    if (!masterRef.current) return;
    for (const {osc, noteIndex, partial} of partialsRef.current) {
      osc.frequency.rampTo(computeFreq(noteIndex, partial, value), 0.04);
    }
  };

  return (
    <div style={{margin: '0.6rem 0'}}>
      <button
        className="button button--primary button--sm"
        style={{marginRight: '0.8rem'}}
        onClick={playing ? stop : play}>
        {playing ? 'Stop' : label}
      </button>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={slider}
        aria-label={caption}
        onChange={(e) => onSlide(parseFloat(e.target.value))}
        style={{verticalAlign: 'middle', width: '14rem'}}
      />
      <code style={{marginLeft: '0.8rem'}}>{caption}</code>
    </div>
  );
}
