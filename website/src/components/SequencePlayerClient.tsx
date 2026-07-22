import React, {useEffect, useRef, useState} from 'react';
import * as Tone from 'tone';
import useBaseUrl from '@docusaurus/useBaseUrl';
import {streamTempos} from '@site/src/lib/rhythm';
import {PIANO_URLS, PIANO_SAMPLE_PATH} from '@site/src/lib/piano';

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
  instrumentToggle?: boolean; // show a Sine/Piano selector (adds the sampled piano); default false
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
  instrumentToggle = false,
  tempoSlider,
}: SequencePlayerProps) {
  const synthRef = useRef<Tone.PolySynth | null>(null);
  const endTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const loopTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const [playing, setPlaying] = useState(false);

  // Optional sampled-piano voice (built lazily on first Play with Piano selected). Shares the
  // exact triggerAttackRelease(freqOrFreqs, dur, at, vel) surface as the sine PolySynth, so the
  // scheduler below is instrument-agnostic. Kept on its own gain node, disposed alongside it.
  const pianoRef = useRef<Tone.Sampler | null>(null);
  const pianoGainRef = useRef<Tone.Gain | null>(null);
  const [instrument, setInstrument] = useState<'sine' | 'piano'>('sine');
  const [pianoLoading, setPianoLoading] = useState(false);
  const sampleBase = useBaseUrl(PIANO_SAMPLE_PATH);

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

  // Lazily build the sampled-piano voice over the C-per-octave Salamander samples; Tone.Sampler
  // pitch-shifts them across the keyboard. Samples fetch on first build — the caller awaits load.
  const getPiano = () => {
    if (!pianoRef.current) {
      pianoGainRef.current = new Tone.Gain(0.5).toDestination();
      pianoRef.current = new Tone.Sampler({
        urls: PIANO_URLS,
        baseUrl: sampleBase,
        release: 0.8,
      }).connect(pianoGainRef.current);
    }
    return pianoRef.current;
  };

  // The active voice for this Play. Both types expose the same triggerAttackRelease/dispose
  // surface, so scheduleCycle and the look-ahead pump treat them identically.
  const getVoice = (): Tone.PolySynth | Tone.Sampler =>
    instrument === 'piano' ? getPiano() : getSynth();

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
    // Same for the piano: dispose to cancel the up-front-scheduled notes (releaseAll would
    // only cut sounding voices). Samples are HTTP-cached, so re-arming next Play is quick.
    pianoRef.current?.dispose();
    pianoRef.current = null;
    pianoGainRef.current?.dispose();
    pianoGainRef.current = null;
    setPlaying(false);
  };

  useEffect(() => () => stop(), []); // dispose on unmount

  // Schedule one pass of the sequence starting at audio time `at`, using the current tempo.
  const scheduleCycle = (
    synth: Tone.PolySynth | Tone.Sampler,
    at: number,
    sec: number,
  ) => {
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
    setPlaying(true);
    const voice = getVoice();
    if (instrument === 'piano') {
      setPianoLoading(true);
      await Tone.loaded(); // wait for sample buffers so the first chord isn't silent
      setPianoLoading(false);
      if (!pianoRef.current) return; // Stop pressed while the samples loaded — bail out
    }
    const t0 = Tone.now() + 0.05; // small lead-in so the first note isn't clipped

    if (loopBeats) {
      // Continuous loop: schedule onset-by-onset a little ahead of the clock so onsets stay
      // sample-accurate (no setInterval drift) and so a slider drag retunes the rhythm *within*
      // a cycle, not just at cycle boundaries. Runs until Stop; the reader gets unlimited time
      // to tap along and settle on a beat.
      const lookAheadSec = 0.3; // schedule this far ahead of the audio clock

      // Flatten one cycle into its onsets, sorted by beat. Coincident onsets (e.g. the
      // polyrhythm's shared downbeat) are separate entries at the same beat — a zero gap.
      const events: {beat: number; fire: (at: number, sec: number) => void}[] = [];
      for (const n of melody) {
        events.push({
          beat: n.beat,
          fire: (at, sec) =>
            voice.triggerAttackRelease(
              keyToFreq(n.key),
              Math.max((n.beats ?? noteBeats) * sec, 0.01),
              at,
              n.velocity ?? 1,
            ),
        });
      }
      for (const c of chords) {
        events.push({
          beat: c.beat,
          fire: (at, sec) =>
            voice.triggerAttackRelease(
              c.keys.map(keyToFreq),
              Math.max(c.beats * sec, 0.01),
              at,
              c.velocity ?? 1,
            ),
        });
      }
      events.sort((a, b) => a.beat - b.beat);

      const N = events.length;
      let i = 0; // absolute onset index across cycles
      let prevBeat = 0; // beat of the last *scheduled* onset (absolute)
      let prevTime = t0; // audio time of that onset
      const pump = () => {
        // Read the live tempo each poll; each not-yet-due onset is (re)computed as
        // prevTime + gap * tempo, anchored on the last onset actually queued — so dragging
        // the slider stretches the rhythm continuously from there, with no phase reset.
        const sec = tempoRef.current;
        while (N > 0) {
          const ev = events[i % N];
          const absBeat = Math.floor(i / N) * loopBeats + ev.beat;
          const at = prevTime + (absBeat - prevBeat) * sec;
          if (at >= Tone.now() + lookAheadSec) break; // not due yet — recompute next poll
          ev.fire(at, sec);
          prevTime = at;
          prevBeat = absBeat;
          i++;
        }
      };
      pump();
      loopTimerRef.current = setInterval(pump, 80); // poll well inside the look-ahead window
      return;
    }

    const sec = tempoRef.current;
    scheduleCycle(voice, t0, sec);
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

  // Optional Sine/Piano selector, shown beside the button only when instrumentToggle is set —
  // so existing embeds (no toggle) render exactly the bare button as before. The choice is read
  // at Play; switching mid-playback takes effect on the next Play.
  const instrumentSelect = instrumentToggle ? (
    <label style={{marginLeft: '0.6rem', fontSize: '0.9em'}}>
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
  ) : null;

  if (!tempoSlider) {
    if (!instrumentToggle) return button; // unchanged: a bare button
    return (
      <div style={{margin: '0.4rem 0'}}>
        <span style={{marginRight: '0.8rem'}}>{button}</span>
        {instrumentSelect}
      </div>
    );
  }

  const {streams, minBeatSec = 0.08, maxBeatSec = 0.32, step = 0.005} = tempoSlider;
  const caption = streamTempos(streams, tempo)
    .map((s) => `${s.count}-beat: ${s.bpm} BPM`)
    .join(' · ');

  return (
    <div style={{margin: '0.4rem 0'}}>
      <span style={{marginRight: '0.8rem'}}>{button}</span>
      {instrumentSelect}
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
