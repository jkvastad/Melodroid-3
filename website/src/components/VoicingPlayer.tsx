import React, {useEffect, useRef, useState} from 'react';
import * as Tone from 'tone';

export type VoicingPlayerProps = {
  // Either fractions on [1, 2) ...
  fractions?: number[];
  // ... or k-tet key indices, converted to ratios 2^(n/k).
  keys?: number[];
  ktet?: number; // default 12
  fundamental?: number; // Hz, default 220
  label?: string;
  mode?: 'chord' | 'arpeggio'; // default 'chord'
  oscillator?: 'sine' | 'triangle' | 'square' | 'sawtooth'; // default 'sine'
  gain?: number; // linear amplitude multiplier, default 1 (e.g. 0.8 = 80%)
};

export default function VoicingPlayer({
  fractions,
  keys,
  ktet = 12,
  fundamental = 220,
  label = 'Play',
  mode = 'chord',
  oscillator = 'sine',
  gain = 1,
}: VoicingPlayerProps) {
  const synthRef = useRef<Tone.PolySynth | null>(null);
  // Output gain the synth runs through, so stop() can ramp the level to zero
  // before disposing — a smooth fade instead of a click from cutting mid-sample.
  const gainRef = useRef<Tone.Gain | null>(null);
  const endTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [playing, setPlaying] = useState(false);

  const ratios = fractions ?? (keys ?? []).map((n) => Math.pow(2, n / ktet));
  const freqs = ratios.map((r) => fundamental * r);

  // Build the synth lazily in the browser; dispose on unmount.
  const getSynth = () => {
    if (!synthRef.current) {
      gainRef.current = new Tone.Gain(1).toDestination();
      synthRef.current = new Tone.PolySynth(Tone.Synth, {
        oscillator: {type: oscillator}, // pure sine = the wave-pattern model
        envelope: {attack: 0.02, decay: 0.1, sustain: 0.8, release: 0.4},
        volume: -12 + Tone.gainToDb(gain), // headroom; PolySynth sums voices
      }).connect(gainRef.current);
    }
    return synthRef.current;
  };

  useEffect(() => () => stop(), []); // dispose on unmount

  const play = async () => {
    await Tone.start(); // unlock audio on user gesture
    const synth = getSynth();
    if (mode === 'arpeggio') {
      // Notes enter one at a time and ring on, accumulating into the full
      // chord; once the last has entered they all sustain together for `hold`
      // seconds, then release as one. Per-note durations are sized so every
      // voice releases at the same absolute time, letting the ear compare the
      // built-up arpeggio against the block chord.
      const step = 0.3; // seconds between successive entries
      const hold = 1.2; // seconds all notes ring together before release
      const t0 = Tone.now();
      const n = freqs.length;
      freqs.forEach((f, i) =>
        synth.triggerAttackRelease(f, (n - i) * step + hold, t0 + i * step),
      );
      // Auto-reset the button when the (finite) arpeggio finishes. Release
      // adds tail beyond the scheduled end, so pad before flipping back.
      const totalSec = n * step + hold;
      endTimerRef.current = setTimeout(
        () => setPlaying(false),
        totalSec * 1000 + 500,
      );
    } else {
      synth.triggerAttack(freqs); // sustained block chord
    }
    setPlaying(true);
  };

  // Ramp the output gain to zero (smooth fade, no click), then dispose once the
  // fade completes. Disposing — not releaseAll — is what actually cancels any
  // future-scheduled arpeggio notes; a disposed synth can't retrigger, so we
  // null the refs and rebuild on the next play.
  const stop = () => {
    if (endTimerRef.current) {
      clearTimeout(endTimerRef.current);
      endTimerRef.current = null;
    }
    const synth = synthRef.current;
    const gainNode = gainRef.current;
    synthRef.current = null;
    gainRef.current = null;
    setPlaying(false);
    if (!synth) return;
    const fadeSec = 0.30;
    gainNode?.gain.rampTo(0, fadeSec); // fade currently-sounding + pending notes
    setTimeout(() => {
      synth.dispose();
      gainNode?.dispose();
    }, fadeSec * 1000 + 50);
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
