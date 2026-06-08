import React, {useEffect, useRef, useState} from 'react';
import * as Tone from 'tone';

// One step of the demo: a chord of fractional-semitone offsets, sounded together
// for `beats` grid beats. A single sustained chord is just a one-event sequence.
export type TimbreEvent = {
  notes: number[]; // fractional semitone offsets from the fundamental
  beats?: number; // duration in grid beats; falls back to 1
};

export type TimbrePlayerProps = {
  events: TimbreEvent[];
  // Partial layout. By default partials follow the article's stretch rule
  //   f_i = f0 * gamma^log2(i+1)
  // with `gamma` = 2.0 harmonic, 2.1 stretched, 1.9 compressed. Supplying
  // `partialRatios` overrides the formula with explicit frequency multipliers
  // (used for the bonang-like inharmonic timbre).
  gamma?: number; // default 2.0
  partials?: number; // default 10
  rolloffDbPerOct?: number; // amplitude rolloff, default 3 dB/octave
  partialRatios?: number[]; // explicit multipliers; overrides gamma when given
  beatSec?: number; // seconds per grid beat; default 1.2
  fundamental?: number; // Hz, default 220
  label?: string; // default 'Play'
  gain?: number; // linear amplitude multiplier; default 0.5
};

// An additive-synthesis player: each note is built from explicit partials, so the
// overtone profile (harmonic / stretched / compressed / inharmonic) is fully under
// our control — something Tone.Synth cannot do (its oscillator types and `custom`
// partials are harmonic-only). This is what lets a fixed interval be heard going
// consonant -> rough purely by reshaping the spectrum (Marjieh et al. 2024).
//
// Lifecycle mirrors the other players: build lazily on a user gesture, track every
// node, and dispose (not just release) on stop/unmount — disposing is the only way
// to cancel oscillators scheduled to start in the future.
export default function TimbrePlayerClient({
  events,
  gamma = 2.0,
  partials = 10,
  rolloffDbPerOct = 3,
  partialRatios,
  beatSec = 1.2,
  fundamental = 220,
  label = 'Play',
  gain = 0.5,
}: TimbrePlayerProps) {
  // Master gain the whole signal runs through, so stop() can fade to zero (no
  // click) before disposing.
  const masterRef = useRef<Tone.Gain | null>(null);
  const nodesRef = useRef<Array<{dispose: () => void}>>([]);
  const endTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [playing, setPlaying] = useState(false);

  const partialCount = partialRatios ? partialRatios.length : partials;

  // Per-partial frequency multiplier and (un-normalised) amplitude.
  const partialFreqRatio = (i: number) =>
    partialRatios ? partialRatios[i] : Math.pow(gamma, Math.log2(i + 1));
  const partialAmp = (i: number) =>
    Math.pow(10, (-rolloffDbPerOct * Math.log2(i + 1)) / 20);
  const ampSum = Array.from({length: partialCount}, (_, i) => partialAmp(i)).reduce(
    (a, b) => a + b,
    0,
  );

  const stop = () => {
    if (endTimerRef.current) {
      clearTimeout(endTimerRef.current);
      endTimerRef.current = null;
    }
    const master = masterRef.current;
    const nodes = nodesRef.current;
    masterRef.current = null;
    nodesRef.current = [];
    setPlaying(false);
    if (!master) return;
    const fadeSec = 0.1;
    master.gain.rampTo(0, fadeSec); // fade sounding + pending partials, no click
    setTimeout(() => {
      nodes.forEach((n) => n.dispose()); // disposing cancels future-scheduled starts
      master.dispose();
    }, fadeSec * 1000 + 50);
  };

  useEffect(() => () => stop(), []); // dispose on unmount

  const play = async () => {
    await Tone.start(); // unlock audio on the user gesture
    const master = new Tone.Gain(1).toDestination();
    masterRef.current = master;
    const nodes: Array<{dispose: () => void}> = [];

    const t0 = Tone.now() + 0.05; // small lead-in so the first attack isn't clipped
    let cursor = 0; // beats elapsed
    const atk = 0.02;
    const rel = 0.08;

    for (const ev of events) {
      const beats = ev.beats ?? 1;
      const dur = Math.max(beats * beatSec, 0.05);
      const start = t0 + cursor * beatSec;
      cursor += beats;

      // One envelope gain per event; partials feed it, it feeds master. Avoids
      // per-oscillator click ramps and keeps the event a single amplitude unit.
      const envGain = new Tone.Gain(0).connect(master);
      const g = envGain.gain;
      g.setValueAtTime(0, start);
      g.linearRampToValueAtTime(1, start + Math.min(atk, dur / 2));
      g.setValueAtTime(1, start + Math.max(dur - rel, dur / 2));
      g.linearRampToValueAtTime(0, start + dur);
      nodes.push(envGain);

      // Split headroom across the simultaneous notes so a chord doesn't clip.
      const noteScale = gain / Math.max(ev.notes.length, 1);
      for (const s of ev.notes) {
        const f0 = fundamental * Math.pow(2, s / 12);
        for (let i = 0; i < partialCount; i++) {
          const pGain = new Tone.Gain(
            (partialAmp(i) / ampSum) * noteScale,
          ).connect(envGain);
          const osc = new Tone.Oscillator(
            f0 * partialFreqRatio(i),
            'sine',
          ).connect(pGain);
          osc.start(start);
          osc.stop(start + dur + 0.05);
          nodes.push(osc, pGain);
        }
      }
    }

    nodesRef.current = nodes;
    setPlaying(true);

    const totalSec = cursor * beatSec;
    endTimerRef.current = setTimeout(
      () => setPlaying(false),
      totalSec * 1000 + 300,
    );
  };

  return (
    <button
      className="button button--primary button--sm"
      style={{margin: '0.4rem 0'}}
      onClick={playing ? stop : play}>
      {playing ? 'Stop' : label}
    </button>
  );
}
