import React, {useEffect, useMemo, useRef, useState} from 'react';
import * as Tone from 'tone';
import PartialSweepPlot from './PartialSweepPlot';
import {
  ampSum as ampSumOf,
  computeFreq as computeFreqOf,
  lockInterval,
  noteOffsets as noteOffsetsOf,
  partialAmp as partialAmpOf,
  partialCount as partialCountOf,
  TimbreConfig,
  TimbreMorphMode,
} from '@site/src/lib/timbrePartials';

export type {TimbreMorphMode};

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
  //   'inharmonic-sweep' — TWO sliders. Slider 2 is the blend t∈[0,1] (as in
  //                  'inharmonic'); slider 1 is the step size s (semitones). The upper
  //                  voice sits at offset s and the inharmonic target is r^i with
  //                  r = 2^(s/12), so moving the step recomputes both the upper note's
  //                  pitch and its matched spectrum (Sethares' construction, generalised
  //                  to any step). `partialRatios` is ignored; `partials` sets N. Step
  //                  axis comes from stepMin/stepMax/stepInitial/stepStep.
  //   'stretch-interval' — TWO sliders. Slider 1 is the interval (the UPPER voice's
  //                  semitone offset, min/max/initial); slider 2 is the stretch γ
  //                  (stepMin/stepMax/stepInitial). The lower voice is fixed at notes[0]
  //                  and partials stretch by γ as in 'gamma'. The sweep map plots partial
  //                  frequency against the interval (semitones on x). notes[1+] are ignored.
  //   'mixed-interval' — slider IS the UPPER voice's semitone offset (like 'interval'), but the
  //                  two voices carry DIFFERENT fixed spectra given by `noteRatios` (one explicit
  //                  partial-ratio array per note). The lower voice is fixed at notes[0]; notes[1+]
  //                  are ignored. Use `rolloffDbPerOct={0}` for equal partial weights. This is the
  //                  mixed-timbre case (e.g. harmonic lower tone vs bonang upper tone).
  mode: TimbreMorphMode;
  notes: number[]; // semitone offsets from the fundamental
  min: number;
  max: number;
  initial: number; // starting slider value
  step?: number; // slider granularity; default 0.01
  // Fixed-spectrum parameters (see mode notes above for which apply).
  gamma?: number; // default 2.0 (used by 'interval')
  partials?: number; // default 10 (used by 'gamma' / 'interval' / 'inharmonic-sweep')
  rolloffDbPerOct?: number; // amplitude rolloff, default 3 dB/octave
  partialRatios?: number[]; // inharmonic target multipliers (used by 'inharmonic')
  noteRatios?: number[][]; // per-note explicit partial multipliers (used by 'mixed-interval')
  scaleMarks?: number[]; // x-positions (semitones) to mark on the sweep map, e.g. slendro steps
  // Step-size slider (used by 'inharmonic-sweep' only).
  stepMin?: number; // semitone range min for the step slider
  stepMax?: number; // semitone range max for the step slider
  stepInitial?: number; // starting step value; defaults to `initial`
  stepStep?: number; // step-slider granularity; default 0.01
  fundamental?: number; // Hz, default 220
  gain?: number; // linear amplitude multiplier; default 0.4
  label?: string; // button label; default 'Play'
  readout?: (m: number) => string; // formats the slider value into a caption
  plot?: boolean; // show the partial-sweep map below the sliders; default true
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
  noteRatios,
  scaleMarks,
  stepMin,
  stepMax,
  stepInitial,
  stepStep = 0.01,
  fundamental = 220,
  gain = 0.4,
  label = 'Play',
  readout,
  plot = true,
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
  const [slider, setSlider] = useState(initial); // axis 1: gamma / interval / blend t
  const [step2, setStep2] = useState(stepInitial ?? initial); // axis 2: step size s
  const [playing, setPlaying] = useState(false);

  // The slider-independent spectrum config, shared with the visual sweep map so the picture
  // and the sound are computed from exactly the same partial formulas (see timbrePartials.ts).
  const cfg: TimbreConfig = useMemo(
    () => ({mode, notes, gamma, partials, rolloffDbPerOct, partialRatios, noteRatios, fundamental}),
    [mode, notes, gamma, partials, rolloffDbPerOct, partialRatios, noteRatios, fundamental],
  );

  // Thin wrappers over the shared math, kept so the rest of this component reads as before.
  // partialCount / ampSum take a note index so 'mixed-interval' can give each voice its own
  // spectrum; every other mode ignores it (same count/sum for all notes).
  const partialCount = (noteIndex = 0) => partialCountOf(cfg, noteIndex);
  const noteOffsets = (t: number, s: number) => noteOffsetsOf(cfg, t, s);
  const computeFreq = (noteIndex: number, partial: number, t: number, s: number) =>
    computeFreqOf(cfg, noteIndex, partial, t, s);
  const partialAmp = (i: number) => partialAmpOf(cfg, i);
  const ampSum = (noteIndex = 0) => ampSumOf(cfg, noteIndex);

  // Two-slider modes expose axis 2 (step2) alongside the main slider.
  const twoSliders = mode === 'inharmonic-sweep' || mode === 'stretch-interval';

  const defaultReadout = (m: number): string => {
    if (mode === 'gamma') return `γ = ${m.toFixed(2)}`;
    if (mode === 'interval' || mode === 'stretch-interval' || mode === 'mixed-interval')
      return `${m.toFixed(2)} st`;
    if (mode === 'inharmonic-sweep')
      return `${Math.round((1 - m) * 100)}% harmonic - ${Math.round(m * 100)}% ratio partials`;
    return `harmonic ↔ inharmonic: ${Math.round(m * 100)}%`;
  };
  const caption = (readout ?? defaultReadout)(slider);
  // axis-2 readout: γ for 'stretch-interval', step size for 'inharmonic-sweep'.
  const stepCaption =
    mode === 'stretch-interval'
      ? `γ = ${step2.toFixed(2)}`
      : `${step2.toFixed(2)} st`;

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

    const offsets = noteOffsets(slider, step2);
    // Split headroom across the simultaneous notes so a chord doesn't clip.
    const noteScale = gain / Math.max(offsets.length, 1);
    offsets.forEach((_, noteIndex) => {
      for (let i = 0; i < partialCount(noteIndex); i++) {
        const pGain = new Tone.Gain(
          (partialAmp(i) / ampSum(noteIndex)) * noteScale,
        ).connect(master);
        const osc = new Tone.Oscillator(
          computeFreq(noteIndex, i, slider, step2),
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

  // Update an axis; if sounding, glide every partial to its new frequency. A short ramp
  // (40 ms) kills zipper noise while staying responsive to dragging. Each handler passes
  // its just-changed axis as a fresh argument and reads the other axis from state (which
  // is correct — only the dragged axis changed in this event; React state is still stale).
  const onSlide = (value: number) => {
    setSlider(value);
    if (!masterRef.current) return;
    for (const {osc, noteIndex, partial} of partialsRef.current) {
      osc.frequency.rampTo(computeFreq(noteIndex, partial, value, step2), 0.04);
    }
  };

  const onSlideStep = (value: number) => {
    setStep2(value);
    if (!masterRef.current) return;
    for (const {osc, noteIndex, partial} of partialsRef.current) {
      osc.frequency.rampTo(computeFreq(noteIndex, partial, slider, value), 0.04);
    }
  };

  // Partial-lock interval (the plot's red dashed line) for the current stretch — the reader's
  // target. Recomputed each render so it tracks the γ slider in 'stretch-interval'; null for modes
  // without a single-γ lock, which hides the match button.
  const lock = lockInterval(cfg, step2);
  // Snap the interval slider onto the lock interval (clamped to the slider range), reusing onSlide
  // so a sounding chord glides to the locked spectrum.
  const matchLock = () => {
    if (lock == null) return;
    onSlide(Math.min(max, Math.max(min, lock)));
  };

  return (
    <div style={{margin: '0.6rem 0'}}>
      <button
        className="button button--primary button--sm"
        style={{marginRight: '0.8rem'}}
        onClick={playing ? stop : play}>
        {playing ? 'Stop' : label}
      </button>
      {/* Two-slider modes ('inharmonic-sweep', 'stretch-interval') put the axis-2 slider
          inline by the button and the axis-1 slider below; every other mode keeps its single
          axis inline. */}
      {twoSliders ? (
        <>
          <input
            type="range"
            min={stepMin}
            max={stepMax}
            step={stepStep}
            value={step2}
            aria-label={stepCaption}
            onChange={(e) => onSlideStep(parseFloat(e.target.value))}
            style={{verticalAlign: 'middle', width: '14rem'}}
          />
          <code style={{marginLeft: '0.8rem'}}>{stepCaption}</code>
          <div style={{marginTop: '0.4rem'}}>
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
            {lock != null && (
              <button
                className="button button--secondary button--sm"
                style={{marginLeft: '0.8rem'}}
                onClick={matchLock}>
                match
              </button>
            )}
          </div>
        </>
      ) : (
        <>
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
          {lock != null && (
            <button
              className="button button--secondary button--sm"
              style={{marginLeft: '0.8rem'}}
              onClick={matchLock}>
              match
            </button>
          )}
        </>
      )}
      {plot && (
        <PartialSweepPlot
          cfg={cfg}
          min={min}
          max={max}
          slider={slider}
          step2={step2}
          marks={scaleMarks}
        />
      )}
    </div>
  );
}
