import React, {useEffect, useRef, useState} from 'react';
import * as Tone from 'tone';
import {streamTempos} from '@site/src/lib/rhythm';

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
  loopBeats?: number; // if set, repeat `melody`/`chords` every loopBeats grid-beats until Stop
  tempoSlider?: {
    streams: number[]; // onset counts per voice, e.g. [2, 3] — labels each stream's BPM
    minBeatSec?: number; // default 0.08
    maxBeatSec?: number; // default 0.32
    step?: number; // default 0.005
  };
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
  loopBeats,
  tempoSlider,
}: SequencePlayerProps) {
  const synthRef = useRef<Tone.PolySynth | null>(null);
  const endTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const loopTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const [playing, setPlaying] = useState(false);

  // Live tempo: starts at the beatSec prop and tracks the slider. A ref mirrors it so the
  // scheduler always reads the current value without restarting the play closure.
  const [tempo, setTempo] = useState(beatSec);
  const tempoRef = useRef(beatSec);
  const setTempoBoth = (s: number) => {
    tempoRef.current = s;
    setTempo(s);
  };

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
    if (loopTimerRef.current) {
      clearInterval(loopTimerRef.current);
      loopTimerRef.current = null;
    }
    synthRef.current?.dispose(); // cancels future scheduled notes + cuts sounding voices
    synthRef.current = null; // a disposed synth can't retrigger; rebuild next play
    setPlaying(false);
  };

  useEffect(() => () => stop(), []); // dispose on unmount

  // Schedule one pass of the sequence starting at audio time `at`, using the current tempo.
  const scheduleCycle = (synth: Tone.PolySynth, at: number, sec: number) => {
    for (const n of melody) {
      const dur = Math.max((n.beats ?? noteBeats) * sec, 0.01);
      synth.triggerAttackRelease(
        keyToFreq(n.key),
        dur,
        at + n.beat * sec,
        n.velocity ?? 1,
      );
    }
    for (const c of chords) {
      const dur = Math.max(c.beats * sec, 0.01);
      synth.triggerAttackRelease(
        c.keys.map(keyToFreq),
        dur,
        at + c.beat * sec,
        c.velocity ?? 1,
      );
    }
  };

  const play = async () => {
    await Tone.start(); // unlock audio on user gesture
    const synth = getSynth();
    const t0 = Tone.now() + 0.05; // small lead-in so the first note isn't clipped
    setPlaying(true);

    if (loopBeats) {
      // Continuous loop: keep an audio-time anchor and schedule successive cycles a little
      // ahead of the clock so onsets stay sample-accurate (no setInterval drift). Runs until
      // Stop. The reader gets unlimited time to tap along and settle on a beat.
      const lookAheadSec = 0.3; // schedule this far ahead of the audio clock
      let nextTime = t0;
      const pump = () => {
        // Read the live tempo each poll so dragging the slider retunes the loop seamlessly:
        // newly scheduled cycles adopt the new tatum while already-queued cycles finish at
        // their old one. nextTime only ever advances by the current cycle length, so there's
        // no phase reset on a tempo change.
        const sec = tempoRef.current;
        const cycleSec = loopBeats * sec;
        // Schedule every cycle whose start falls within the look-ahead window. The window
        // exceeds the poll interval so each cycle is queued well before it must sound.
        while (nextTime < Tone.now() + lookAheadSec) {
          scheduleCycle(synth, nextTime, sec);
          nextTime += cycleSec;
        }
      };
      pump();
      loopTimerRef.current = setInterval(pump, 80); // poll well inside the look-ahead window
      return;
    }

    const sec = tempoRef.current;
    scheduleCycle(synth, t0, sec);
    const endBeat = Math.max(
      0,
      ...melody.map((n) => n.beat + (n.beats ?? noteBeats)),
      ...chords.map((c) => c.beat + c.beats),
    );
    endTimerRef.current = setTimeout(
      () => setPlaying(false),
      endBeat * sec * 1000 + 200,
    );
  };

  const button = (
    <button
      className="button button--primary button--sm"
      style={{margin: '0.4rem 0'}}
      onClick={playing ? stop : play}>
      {playing ? 'Stop' : label}
    </button>
  );

  if (!tempoSlider) return button;

  const {streams, minBeatSec = 0.08, maxBeatSec = 0.32, step = 0.005} = tempoSlider;
  const caption = streamTempos(streams, tempo)
    .map((s) => `${s.count}-beat: ${s.bpm} BPM`)
    .join(' · ');

  return (
    <div style={{margin: '0.4rem 0'}}>
      <span style={{marginRight: '0.8rem'}}>{button}</span>
      <input
        type="range"
        min={minBeatSec}
        max={maxBeatSec}
        step={step}
        // Slider runs fast→slow left→right (larger beatSec = slower tatum); flipping the
        // value keeps the conventional left=slow, right=fast feel. A running loop reads the
        // tempo live each poll, so dragging retunes it immediately; when stopped the new
        // value simply takes effect on the next Play.
        value={minBeatSec + maxBeatSec - tempo}
        aria-label={caption}
        onChange={(e) =>
          setTempoBoth(minBeatSec + maxBeatSec - parseFloat(e.target.value))
        }
        style={{verticalAlign: 'middle', width: '14rem'}}
      />
      <code style={{marginLeft: '0.8rem'}}>{caption}</code>
    </div>
  );
}
