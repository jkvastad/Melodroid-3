import React, {useEffect, useRef, useState} from 'react';
import * as Tone from 'tone';
import useBaseUrl from '@docusaurus/useBaseUrl';
import {PIANO_URLS, PIANO_SAMPLE_PATH} from '@site/src/lib/piano';

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
  instrumentToggle?: boolean; // show a Sine/Piano selector (adds the sampled piano); default false
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
  instrumentToggle = false,
}: VoicingPlayerProps) {
  const synthRef = useRef<Tone.PolySynth | null>(null);
  // Output gain the synth runs through, so stop() can ramp the level to zero
  // before disposing — a smooth fade instead of a click from cutting mid-sample.
  const gainRef = useRef<Tone.Gain | null>(null);
  const endTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [playing, setPlaying] = useState(false);

  // Optional sampled-piano voice (built lazily on first Play with Piano selected). Shares the
  // exact triggerAttack / triggerAttackRelease surface as the sine PolySynth, so the chord and
  // arpeggio scheduling below is instrument-agnostic. Kept on its own gain node for the fade.
  const pianoRef = useRef<Tone.Sampler | null>(null);
  const pianoGainRef = useRef<Tone.Gain | null>(null);
  const [instrument, setInstrument] = useState<'sine' | 'piano'>('sine');
  const [pianoLoading, setPianoLoading] = useState(false);
  const sampleBase = useBaseUrl(PIANO_SAMPLE_PATH);

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

  // Lazily build the sampled-piano voice over the minor-third Salamander samples; Tone.Sampler
  // pitch-shifts them across the keyboard. Runs through its own gain node so stop() can fade it.
  const getPiano = () => {
    if (!pianoRef.current) {
      pianoGainRef.current = new Tone.Gain(1).toDestination();
      pianoRef.current = new Tone.Sampler({
        urls: PIANO_URLS,
        baseUrl: sampleBase,
        release: 0.8,
      }).connect(pianoGainRef.current);
    }
    return pianoRef.current;
  };

  // The active voice for this Play. Both types expose the same triggerAttack /
  // triggerAttackRelease surface, so the scheduling in play() treats them identically.
  const getVoice = (): Tone.PolySynth | Tone.Sampler =>
    instrument === 'piano' ? getPiano() : getSynth();

  useEffect(() => () => stop(), []); // dispose on unmount

  const play = async () => {
    await Tone.start(); // unlock audio on user gesture
    const synth = getVoice();
    if (instrument === 'piano') {
      setPianoLoading(true);
      await Tone.loaded(); // wait for sample buffers so the first note isn't silent
      setPianoLoading(false);
      if (!pianoRef.current) return; // Stop pressed while samples loaded — bail out
    }
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

  // Ramp the active voice's output gain to zero (smooth fade, no click), then dispose once the
  // fade completes. Disposing — not releaseAll — is what actually cancels any future-scheduled
  // arpeggio notes; a disposed voice can't retrigger, so we null the refs and rebuild on the next
  // play. Both the sine synth and the piano sampler are torn down the same way.
  const stop = () => {
    if (endTimerRef.current) {
      clearTimeout(endTimerRef.current);
      endTimerRef.current = null;
    }
    const synth = synthRef.current;
    const synthGain = gainRef.current;
    const piano = pianoRef.current;
    const pianoGain = pianoGainRef.current;
    synthRef.current = null;
    gainRef.current = null;
    pianoRef.current = null;
    pianoGainRef.current = null;
    setPlaying(false);
    const fadeSec = 0.30;
    const fade = (
      voice: Tone.PolySynth | Tone.Sampler | null,
      gainNode: Tone.Gain | null,
    ) => {
      if (!voice) return;
      gainNode?.gain.rampTo(0, fadeSec); // fade currently-sounding + pending notes
      setTimeout(() => {
        voice.dispose();
        gainNode?.dispose();
      }, fadeSec * 1000 + 50);
    };
    fade(synth, synthGain);
    fade(piano, pianoGain);
  };

  const button = (
    <button
      className="button button--primary button--sm"
      style={{margin: '0.4rem 0'}}
      onClick={playing ? stop : play}>
      {playing ? 'Stop' : label}
    </button>
  );

  if (!instrumentToggle) return button; // unchanged: a bare button

  // Sine/Piano selector, shown beside the button only when instrumentToggle is set — so existing
  // embeds (no toggle) render exactly the bare button as before. The choice is read at Play;
  // switching mid-playback takes effect on the next Play.
  return (
    <span style={{display: 'inline-flex', alignItems: 'center', gap: '0.6rem'}}>
      {button}
      <label style={{fontSize: '0.9em'}}>
        instrument{' '}
        <select
          value={instrument}
          onChange={(e) => setInstrument(e.target.value as 'sine' | 'piano')}
          aria-label="instrument timbre">
          <option value="sine">Sine</option>
          <option value="piano">Piano</option>
        </select>
        {pianoLoading && (
          <span style={{marginLeft: '0.4rem', opacity: 0.7, fontStyle: 'italic'}}>
            loading…
          </span>
        )}
      </label>
    </span>
  );
}
