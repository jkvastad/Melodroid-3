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
  const [playing, setPlaying] = useState(false);

  const ratios = fractions ?? (keys ?? []).map((n) => Math.pow(2, n / ktet));
  const freqs = ratios.map((r) => fundamental * r);

  // Build the synth lazily in the browser; dispose on unmount.
  const getSynth = () => {
    if (!synthRef.current) {
      synthRef.current = new Tone.PolySynth(Tone.Synth, {
        oscillator: {type: oscillator}, // pure sine = the wave-pattern model
        envelope: {attack: 0.02, decay: 0.1, sustain: 0.8, release: 0.4},
        volume: -12 + Tone.gainToDb(gain), // headroom; PolySynth sums voices
      }).toDestination();
    }
    return synthRef.current;
  };

  useEffect(
    () => () => {
      synthRef.current?.dispose();
    },
    [],
  );

  const play = async () => {
    await Tone.start(); // unlock audio on user gesture
    const synth = getSynth();
    if (mode === 'arpeggio') {
      const t0 = Tone.now();
      freqs.forEach((f, i) =>
        synth.triggerAttackRelease(f, '8n', t0 + i * 0.25),
      );
    } else {
      synth.triggerAttack(freqs); // sustained block chord
      setPlaying(true);
    }
  };

  const stop = () => {
    synthRef.current?.releaseAll();
    setPlaying(false);
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
