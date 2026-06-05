import React, {useEffect, useRef, useState} from 'react';
import * as Tone from 'tone';

// A scheduled melody/chord note, addressed in k-tet keys and grid beats.
export type SequenceNote = {
  key: number; // k-tet key index (may be negative)
  beat: number; // onset, in grid beats
  beats?: number; // duration in beats; falls back to noteBeats
  velocity?: number; // 0..1, default 1
};
export type SequenceChord = {
  keys: number[]; // sounded together
  beat: number; // onset, in grid beats
  beats: number; // duration in beats
  velocity?: number; // 0..1, default 1
};

export type SequencePlayerProps = {
  melody?: SequenceNote[];
  chords?: SequenceChord[];
  beatSec?: number; // seconds per grid beat; default 0.25
  noteBeats?: number; // fallback note duration when `beats` omitted; default 1
  ktet?: number; // default 12
  fundamental?: number; // Hz, default 220
  label?: string; // default 'Play'
  oscillator?: 'sine' | 'triangle' | 'square' | 'sawtooth'; // default 'sine'
  gain?: number; // linear amplitude multiplier; default 0.6 (chord + melody stack)
};

// Plays a timed sequence of notes/chords (the site's first non-sustained player).
// Scheduling mirrors VoicingPlayer's `arpeggio` mode: everything is queued up front
// off Tone.now() + offset. Stop disposes the synth (the only reliable way to cancel
// pre-scheduled notes — releaseAll only releases currently-sounding voices).
export default function SequencePlayerClient({
  melody = [],
  chords = [],
  beatSec = 0.25,
  noteBeats = 1,
  ktet = 12,
  fundamental = 220,
  label = 'Play',
  oscillator = 'sine',
  gain = 0.6,
}: SequencePlayerProps) {
  const synthRef = useRef<Tone.PolySynth | null>(null);
  const endTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [playing, setPlaying] = useState(false);

  const keyToFreq = (k: number) => fundamental * Math.pow(2, k / ktet);

  const getSynth = () => {
    if (!synthRef.current) {
      synthRef.current = new Tone.PolySynth(Tone.Synth, {
        oscillator: {type: oscillator}, // pure sine = the wave-pattern model
        // Release shortened from VoicingPlayer's 0.4 so the fast gallop stays
        // distinct; the sustained chord still rings via its 0.8 sustain stage.
        envelope: {attack: 0.02, decay: 0.1, sustain: 0.8, release: 0.2},
        volume: -12 + Tone.gainToDb(gain), // headroom; PolySynth sums voices
      }).toDestination();
    }
    return synthRef.current;
  };

  const stop = () => {
    if (endTimerRef.current) {
      clearTimeout(endTimerRef.current);
      endTimerRef.current = null;
    }
    synthRef.current?.dispose(); // cancels future scheduled notes + cuts sounding voices
    synthRef.current = null; // a disposed synth can't retrigger; rebuild next play
    setPlaying(false);
  };

  useEffect(() => () => stop(), []); // dispose on unmount

  const play = async () => {
    await Tone.start(); // unlock audio on user gesture
    const synth = getSynth();
    const t0 = Tone.now() + 0.05; // small lead-in so the first note isn't clipped
    for (const n of melody) {
      const dur = Math.max((n.beats ?? noteBeats) * beatSec, 0.01);
      synth.triggerAttackRelease(
        keyToFreq(n.key),
        dur,
        t0 + n.beat * beatSec,
        n.velocity ?? 1,
      );
    }
    for (const c of chords) {
      const dur = Math.max(c.beats * beatSec, 0.01);
      synth.triggerAttackRelease(
        c.keys.map(keyToFreq),
        dur,
        t0 + c.beat * beatSec,
        c.velocity ?? 1,
      );
    }
    const endBeat = Math.max(
      0,
      ...melody.map((n) => n.beat + (n.beats ?? noteBeats)),
      ...chords.map((c) => c.beat + c.beats),
    );
    setPlaying(true);
    endTimerRef.current = setTimeout(
      () => setPlaying(false),
      endBeat * beatSec * 1000 + 200,
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
