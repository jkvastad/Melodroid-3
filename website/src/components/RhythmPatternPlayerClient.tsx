import React, {useEffect, useMemo, useRef, useState} from 'react';
import * as Tone from 'tone';
import useBaseUrl from '@docusaurus/useBaseUrl';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import {
  generatePattern,
  gridLines,
  mulberry32,
  parseMeter,
  parseSubdivisions,
  type Pulse,
} from '@site/src/lib/rhythmPattern';
import {findSupersets, type Superset} from '@site/src/lib/placements';
import {enumerateAll, type Voicing} from '@site/src/lib/voicings';

export type RhythmPatternPlayerProps = {
  meter?: string; // initial meter, e.g. '4' or '7 2 3'; default '4'
  subdivisions?: string; // initial subdivision spec; default '2'
  bpm?: number; // initial unit-beat tempo; default 100
  minBpm?: number; // tempo slider bounds; default 40 / 240
  maxBpm?: number;
  syncopation?: number; // initial [0,1]; default 0
  resolution?: number; // initial [0,1]; default 1
  pitchHz?: number; // fixed blip pitch in Hz; default 165
  height?: number; // plot height in px; default 240
  melody?: boolean; // show the lcm-family melody controls (this page only); default false
  chord?: boolean; // chord mode: roll a random chord, find the LCM families whose placement
  // contains it, draw melody from a matched family, and sound the chord as long notes
  // re-struck each meter group (this page only); default false
};

// Chord mode only auditions LCM families within the study range; larger folded LCMs
// (a chord can sit inside placements of much larger families) are not offered as
// interpretations. Passed to findSupersets as its maxLcm.
const MAX_CHORD_LCM = 24;

// Chord-mode accompaniment floor: the voicing is dropped an octave below the melody, so its
// bottom note (its root) can land very low. Skip to the next-best voicing whose bottom note
// clears this (~C3) rather than let the chord get muddy.
const MIN_CHORD_HZ = 130;

// Sampled-piano note map for the optional "Piano" instrument. A small C-per-octave subset
// of the Salamander Grand Piano (see static/samples/piano/NOTICE.txt); Tone.Sampler
// pitch-shifts these across the keyboard, so a few files cover the whole range. Fetched
// lazily on the first Play with Piano selected — nothing downloads for the sine default.
const PIANO_URLS: Record<string, string> = {
  C2: 'C2.mp3',
  C3: 'C3.mp3',
  C4: 'C4.mp3',
  C5: 'C5.mp3',
  C6: 'C6.mp3',
};

// Roll a random chord (2–7 distinct chromatic keys) and find its superset placements, retrying
// until it is a subset of at least one LCM family placement with LCM ≤ MAX_CHORD_LCM. Returns
// the chord together with those matches (ranked tightest-first). A large but bounded retry cap
// guards against the rare no-match draw; the [0,4,7] major triad is a guaranteed-legal fallback
// if the cap is ever hit.
function rollChord(rng: () => number): {keys: number[]; matches: Superset[]} {
  for (let attempt = 0; attempt < 500; attempt++) {
    const size = 2 + Math.floor(rng() * 6); // 2..7
    const pool = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
    // Partial Fisher–Yates: take the first `size` after shuffling those slots.
    for (let i = 0; i < size; i++) {
      const j = i + Math.floor(rng() * (pool.length - i));
      [pool[i], pool[j]] = [pool[j], pool[i]];
    }
    const keys = pool.slice(0, size).sort((a, b) => a - b);
    const matches = findSupersets(keys, 12, MAX_CHORD_LCM);
    if (matches.length > 0) return {keys, matches};
  }
  const keys = [0, 4, 7];
  return {keys, matches: findSupersets(keys, 12, MAX_CHORD_LCM)};
}

// Parse a user-typed chord ("0 4 7" / "0,4,7") into distinct pitch classes 0..11, sorted
// ascending (matching how rollChord sorts before sweeping). Throws Error with a friendly
// message on bad input so the client can show an inline hint and keep the last valid chord.
// The 2..7 count mirrors rollChord's range — no LCM family exceeds 7 members.
function parseChordKeys(text: string): number[] {
  const tokens = text.split(/[\s,]+/).filter((t) => t.length > 0);
  const keys = tokens.map((t) => {
    if (!/^\d+$/.test(t)) throw new Error(`"${t}" is not a whole number.`);
    const n = parseInt(t, 10);
    if (n < 0 || n > 11) throw new Error(`Keys must be 0–11, got ${n}.`);
    return n;
  });
  if (new Set(keys).size !== keys.length)
    throw new Error('Chord keys must be distinct.');
  if (keys.length < 2 || keys.length > 7)
    throw new Error('Enter 2–7 keys, e.g. "0 4 7".');
  return [...keys].sort((a, b) => a - b);
}

// Voice a chord as semitone offsets from pitchHz (key 0 = pitchHz, the melody's root):
// an ascending, semitone-avoiding voicing with its root dropped one octave below the melody
// octave (the `- 12`), keeping each note's pitch class aligned with the melody. Normally the
// lowest-penalty voicing, but the octave drop can push its bottom note (always its root) very
// low, so we skip to the next-best voicing whose bottom note clears MIN_CHORD_HZ. When no
// voicing clears it (all chord keys are low) we take the highest-rooted one (least low). Falls
// back to the raw chord placed an octave below when no semitone-free voicing exists at all
// (the rare all-semitone chord, e.g. a bare semitone dyad).
function chordOffsets(keys: number[] | null, pitchHz: number): number[] | null {
  if (!keys || keys.length === 0) return null;
  const voicings = enumerateAll(keys).sort(
    (a, b) => a.penalty - b.penalty || a.span - b.span,
  );
  if (voicings.length === 0) return keys.map((k) => k - 12); // all-semitone chord
  // Bottom note is the root, dropped one octave; keep it at/above the threshold.
  const clears = (v: Voicing) =>
    pitchHz * Math.pow(2, (v.root - 12) / 12) >= MIN_CHORD_HZ;
  const pick =
    voicings.find(clears) ??
    voicings.reduce((a, b) => (b.root > a.root ? b : a)); // none clears: least low
  return pick.offsets.map((off) => pick.root - 12 + off);
}

// A match labelled for the dropdown, e.g. "24 @ 0" (LCM family 24 anchored at key 0).
const matchLabel = (m: Superset): string => `${m.lcm} @ ${m.at}`;

// The LCM families of the intro table on voicings-and-lcm-families.mdx, keyed to that
// table's rows. `keys` holds the raw table voicing (so the provenance is visible); the
// player folds them into a single octave before drawing pitches from them. The leading
// id '0' is not a family: it is the chromatic draw pool — all 12 pitch classes, a uniform
// random draw over the whole octave rather than a good-fraction subset. The RANDOM_ID
// entry is not a family either: it draws a continuous frequency anywhere in the octave
// (a real key in [0,12), unquantized) to contrast truly random pitch against the 12 keys;
// its `keys` is unused (the octaveKeys memo short-circuits on RANDOM_ID).
const RANDOM_ID = 'random';
type LcmFamily = {id: string; label: string; keys: number[]};
const LCM_FAMILIES: LcmFamily[] = [
  {id: '0', label: '0 · Chromatic', keys: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]},
  {id: RANDOM_ID, label: '∅ · Random Pitch', keys: []},
  {id: 'major-pentatonic', label: 'Major Pentatonic', keys: [0, 2, 4, 7, 9]},
  {id: 'harmonic-minor', label: 'Harmonic Minor', keys: [0, 1, 4, 5, 7, 8, 10]},
  {id: '1', label: '1 · Unison', keys: [0]},
  {id: '2', label: '2 · Perfect Fifth', keys: [0, 7]},
  {id: '3,4', label: '3,4 · Major Third', keys: [0, 4, 7]},
  {id: '5,6', label: '5,6 · Add 9', keys: [0, 2, 4, 7]},
  {id: '8,9,10,12', label: '8,9,10,12 · Major 9', keys: [0, 4, 7, 11, 14]},
  {id: '15', label: '15', keys: [0, 3, 7, 11, 14, 17, 22]},
  {id: '18', label: '18 · Minor 11', keys: [0, 3, 7, 10, 14, 17]},
  {id: '20', label: '20', keys: [0, 4, 8, 11, 15, 18]},
  {id: '24', label: '24 · Major 13', keys: [0, 4, 7, 10, 14, 17, 21]},
];

// Fold a voicing into one octave of distinct pitch classes [0,12), sorted low → high —
// "an octave of pitches comprising the lcm family" for the melody to draw from.
const foldOctave = (keys: number[]): number[] =>
  [...new Set(keys.map((k) => ((k % 12) + 12) % 12))].sort((a, b) => a - b);

// The pulses that actually sound, in the order the scheduler fires them. Factored so the
// baked melody assigns one key per event in exactly that order (play() reuses this).
const firingEvents = (pulses: Pulse[]): Pulse[] =>
  pulses.filter((p) => p.velocity > 0).sort((a, b) => a.unitBeat - b.unitBeat);

// Index into `pulses` of each firing event, in firing order — the bridge from a per-event
// key list (bakedKeys / a live loop-off roll) back to the per-bar color array, which is
// indexed by position in the full `pulses` array. Reference identity is safe because
// firingEvents just filters the same objects out of `pulses`.
const firingPulseIndices = (pulses: Pulse[]): number[] => {
  const idxOf = new Map<Pulse, number>(pulses.map((p, i) => [p, i]));
  return firingEvents(pulses).map((e) => idxOf.get(e)!);
};

// Default (no-melody) bar colors — the original single blue used before pitch coloring.
const BLUE_FILL = 'rgba(30,90,168,0.55)';
const BLUE_STROKE = 'rgba(30,90,168,0.95)';

// Map a pitch class to a visible-spectrum colour: low pitch (long wavelength) → red,
// high pitch → blue/violet. Hue 0° (red) … 285° (violet) across the octave; the pitch
// class is folded into [0,12) first so any raw key lands somewhere on the gradient.
const spectrumHue = (key: number): number => ((((key % 12) + 12) % 12) / 12) * 285;
const pitchFill = (key: number): string => `hsla(${spectrumHue(key)}, 85%, 55%, 0.6)`;
const pitchStroke = (key: number): string => `hsl(${spectrumHue(key)}, 85%, 45%)`;

// The same red→violet ramp as the bars, as a CSS gradient for the plot's legend swatch:
// one stop per pitch class 0…11 so the legend gradient matches the bar colours exactly.
const SPECTRUM_GRADIENT = `linear-gradient(to right, ${Array.from(
  {length: 12},
  (_, k) => `hsl(${spectrumHue(k)}, 85%, 55%)`,
).join(', ')})`;

// A rendered pattern together with the meter/subdivisions it was built from, so the
// plot's grid lines and x-range always match the bars (both only change on Generate).
type RenderedPattern = {
  pulses: Pulse[];
  totalBeats: number;
  meter: number[];
  subdivisions: number[];
};

type GridSpec = ReturnType<typeof gridLines>;

// Background grid drawn behind the bars (drawClear fires before the series): faint at
// every pulse, medium at every unit beat, bold at the meter accents. Mirrors the
// vertical-line plugins in WavePlotClient / PartialSweepPlot.
function gridPlugin(lines: GridSpec): uPlot.Plugin {
  const stroke = (u: uPlot, xs: number[], color: string, width: number) => {
    const {ctx} = u;
    ctx.beginPath();
    ctx.lineWidth = width;
    ctx.strokeStyle = color;
    const top = u.bbox.top;
    const bot = u.bbox.top + u.bbox.height;
    for (const x of xs) {
      const cx = Math.round(u.valToPos(x, 'x', true));
      ctx.moveTo(cx, top);
      ctx.lineTo(cx, bot);
    }
    ctx.stroke();
  };
  return {
    hooks: {
      drawClear: (u) => {
        u.ctx.save();
        stroke(u, lines.pulses, 'rgba(120,120,140,0.18)', 1);
        stroke(u, lines.unitBeats, 'rgba(90,90,120,0.4)', 1);
        stroke(u, lines.groupStarts, 'rgba(40,40,70,0.7)', 2);
        u.ctx.restore();
      },
    },
  };
}

// "Sing-along" playhead: marks the latest-sounded bar during playback. Drawn on the `draw`
// hook (after the series, so it sits on top of the bars). `beatRef` holds the current bar's
// unitBeat x, or null when stopped. Reads the ref at draw time so a cheap redraw() moves it.
function playheadPlugin(beatRef: {current: number | null}): uPlot.Plugin {
  const FILL = '#f08c00'; // amber — reads over both the blue and spectrum bars, either theme
  const STROKE = 'rgba(80,40,0,0.9)';
  return {
    hooks: {
      draw: (u) => {
        const beat = beatRef.current;
        if (beat == null) return;
        const cx = Math.round(u.valToPos(beat, 'x', true));
        const top = u.bbox.top;
        const base = u.bbox.top + u.bbox.height; // baseline (velocity 0), foot of the bars
        const {ctx} = u;
        ctx.save();
        // Faint vertical guide through the current bar.
        ctx.beginPath();
        ctx.lineWidth = 1;
        ctx.strokeStyle = 'rgba(240,140,0,0.35)';
        ctx.moveTo(cx, top);
        ctx.lineTo(cx, base);
        ctx.stroke();
        // Upward-pointing triangle in the bottom margin, apex at the baseline.
        const half = 6;
        const h = 9;
        ctx.beginPath();
        ctx.moveTo(cx, base); // apex (points up at the bar)
        ctx.lineTo(cx - half, base + h);
        ctx.lineTo(cx + half, base + h);
        ctx.closePath();
        ctx.fillStyle = FILL;
        ctx.fill();
        ctx.lineWidth = 1;
        ctx.strokeStyle = STROKE;
        ctx.stroke();
        ctx.restore();
      },
    },
  };
}

// syncopation and resolution live in [0,1]; clamp typed entries into range.
const clampUnit = (x: number): number => Math.max(0, Math.min(1, x));

// Render a unit value with at least one decimal place so the number boxes read as
// fractional (0 → "0.0", 1 → "1.0") without truncating finer slider steps
// (0.37 stays "0.37").
function fmtUnit(x: number): string {
  const s = String(x);
  return s.includes('.') ? s : s + '.0';
}

export default function RhythmPatternPlayerClient({
  meter: meterProp = '4',
  subdivisions: subProp = '2',
  bpm: bpmProp = 100,
  minBpm = 40,
  maxBpm = 240,
  syncopation: syncProp = 0,
  resolution: resProp = 1,
  pitchHz = 196,
  height = 240,
  melody = false,
  chord = false,
}: RhythmPatternPlayerProps) {
  // Parse the author-supplied defaults once, falling back to a sane starter if the
  // MDX passes something malformed.
  const initial = useMemo(() => {
    let m: number[];
    try {
      m = parseMeter(meterProp);
    } catch {
      m = [4];
    }
    let s: number[];
    try {
      s = parseSubdivisions(subProp, m.length);
    } catch {
      s = Array<number>(m.length).fill(2);
    }
    return {m, s};
  }, [meterProp, subProp]);

  // Controls: text mirrors the input box; the parsed value is the last *valid* parse.
  const [meterText, setMeterText] = useState(meterProp);
  const [meter, setMeter] = useState<number[]>(initial.m);
  const [meterError, setMeterError] = useState<string | null>(null);
  const [subText, setSubText] = useState(subProp);
  const [subdivisions, setSubdivisions] = useState<number[]>(initial.s);
  const [subError, setSubError] = useState<string | null>(null);
  const [syncopation, setSyncopation] = useState(syncProp);
  const [syncText, setSyncText] = useState(() => fmtUnit(syncProp));
  const [resolution, setResolution] = useState(resProp);
  const [resText, setResText] = useState(() => fmtUnit(resProp));
  const [seed, setSeed] = useState(1);
  const [pattern, setPattern] = useState<RenderedPattern | null>(null);
  const [playing, setPlaying] = useState(false);

  // Instrument timbre: the theory-faithful sine/triangle synths (default) or a sampled
  // piano. Piano governs both the melody blips and (in chord mode) the chord accompaniment.
  // Read at Play time — changing it mid-playback applies on the next Play. `pianoLoading`
  // gates the first Play with Piano selected while the samples fetch.
  const [instrument, setInstrument] = useState<'sine' | 'piano'>('sine');
  const [pianoLoading, setPianoLoading] = useState(false);

  // Melody (only surfaced when `melody`): which intro-table LCM family to draw pitches
  // from ('' = fixed pitch, today's behavior), and whether the drawn pitches are baked
  // into a repeating phrase (loop on) or re-rolled on every hit (loop off).
  const [selectedLcm, setSelectedLcm] = useState(melody ? '8,9,10,12' : '');
  const [loopMelody, setLoopMelody] = useState(true);

  // Chord mode (only when `chord`): a randomly rolled chord together with the LCM family
  // placements that contain it as a subset, and which of those matches drives the melody. The
  // chord itself is re-rolled only on Generate; switching selectedMatchIdx re-interprets the
  // same chord as a different family (the "ambiguous context" of the surrounding prose).
  const [chordState, setChordState] = useState<{
    keys: number[];
    matches: Superset[];
  } | null>(null);
  const [selectedMatchIdx, setSelectedMatchIdx] = useState(0);
  // The chord input box: text mirrors what's typed; chordError holds an inline validation
  // message (the last valid chordState is retained on a bad edit).
  const [chordText, setChordText] = useState('');
  const [chordError, setChordError] = useState<string | null>(null);

  // Live tempo: bpm drives the UI, tempoRef (seconds per unit beat) is read by the
  // scheduler each poll so a slider/number change retunes a running loop immediately.
  const [bpm, setBpm] = useState(bpmProp);
  const tempoRef = useRef(60 / bpmProp);
  const setBpmBoth = (b: number) => {
    const clamped = Math.max(minBpm, Math.min(maxBpm, Math.round(b)));
    tempoRef.current = 60 / clamped;
    setBpm(clamped);
  };

  const synthRef = useRef<Tone.Synth | null>(null);
  const gainRef = useRef<Tone.Gain | null>(null);
  // Chord-mode accompaniment: a polyphonic synth sounding the chord as long notes,
  // re-struck at each meter group start, beneath the melody blips.
  const chordSynthRef = useRef<Tone.PolySynth | null>(null);
  const chordGainRef = useRef<Tone.Gain | null>(null);
  // Sampled-piano voices for the "Piano" instrument (one for melody, one for the chord pad),
  // each on its own persistent gain node. Unlike the sine synths (rebuilt each Play), these
  // are built once and cached for the component's life so Sine↔Piano and Stop↔Play never
  // re-download or re-decode; disposed only on unmount. Sample paths honour the site baseUrl.
  const pianoMelodyRef = useRef<Tone.Sampler | null>(null);
  const pianoMelodyGainRef = useRef<Tone.Gain | null>(null);
  const pianoChordRef = useRef<Tone.Sampler | null>(null);
  const pianoChordGainRef = useRef<Tone.Gain | null>(null);
  const sampleBase = useBaseUrl('/samples/piano/');
  const loopTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const plotRef = useRef<uPlot | null>(null);

  // Per-bar spectrum colours, indexed by position in the current pattern's `pulses`
  // (null ⇒ fall back to flat blue). The uPlot bar series reads these via `disp`; the
  // baked-colour effect and the live loop-off scheduler write them and cheap-redraw.
  const fillColorsRef = useRef<string[] | null>(null);
  const strokeColorsRef = useRef<string[] | null>(null);

  // Sing-along playhead: the current bar's unitBeat (null when stopped), read by
  // playheadPlugin. playRunRef tags each play() run so scheduled callbacks left in flight
  // after Stop/Generate no-op — the global Tone.getDraw() is shared by both players on the
  // page, so we invalidate per-instance by run id rather than cancelling it globally.
  const playheadBeatRef = useRef<number | null>(null);
  const playRunRef = useRef(0);

  // --- Melody: pitch pool + per-event pitch assignment ---

  // Random Pitch draws a continuous key in [0,12) rather than a discrete family pool
  // (never in chord mode, which always draws from a chord-matched family).
  const isRandomPitch = !chord && selectedLcm === RANDOM_ID;
  // The pitch pool folded to one octave, or null for fixed pitch / random pitch. In chord
  // mode it is the selected match's LCM family placement (a superset of the chord); in
  // melody mode it is the chosen intro-table family.
  const octaveKeys = useMemo(() => {
    if (chord) {
      const m = chordState?.matches[selectedMatchIdx];
      return m ? foldOctave(m.keys) : null;
    }
    if (selectedLcm === RANDOM_ID) return null;
    const fam = LCM_FAMILIES.find((f) => f.id === selectedLcm);
    return fam ? foldOctave(fam.keys) : null;
  }, [chord, chordState, selectedMatchIdx, selectedLcm]);
  // Melody is active (pitch varies + bars are spectrum-coloured) for a family or random
  // pitch — the single flag that replaces the old `octaveKeys`-truthiness tests.
  const melodyOn = isRandomPitch || octaveKeys != null;

  // Loop-on melody: one random key per firing event, drawn with the same seeded RNG as
  // the rhythm so a given (pattern, family, seed) always yields the same phrase. Re-rolls
  // when the rhythm (pattern/seed) or the family changes; null when fixed pitch.
  const bakedKeys = useMemo(() => {
    if (!pattern || !melodyOn) return null;
    const rng = mulberry32(seed);
    return firingEvents(pattern.pulses).map(() =>
      isRandomPitch ? rng() * 12 : octaveKeys![Math.floor(rng() * octaveKeys!.length)],
    );
  }, [pattern, melodyOn, isRandomPitch, octaveKeys, seed]);

  // Mirror the melody config into refs so the look-ahead scheduler (play's pump) reads the
  // current values live, exactly like tempoRef — switching family / loop retunes a running
  // loop without a replay.
  const octaveKeysRef = useRef(octaveKeys);
  const bakedKeysRef = useRef(bakedKeys);
  const loopMelodyRef = useRef(loopMelody);
  const melodyOnRef = useRef(melodyOn);
  const isRandomPitchRef = useRef(isRandomPitch);
  // The chord's voicing as semitone offsets from pitchHz, read by the scheduler so the
  // chord follows a newly generated one without a replay (mirrors octaveKeysRef): an
  // ascending, semitone-avoiding ordering (see §Scoring voicings) with its root dropped one
  // octave below the melody octave, so the chord underpins the melody instead of clustering on
  // top of it — the lowest-penalty such voicing whose bottom note clears MIN_CHORD_HZ (see
  // chordOffsets). Precomputed on a chord change so the scheduler need not re-voice per hit.
  const chordVoicingRef = useRef<number[] | null>(
    chordOffsets(chordState?.keys ?? null, pitchHz),
  );
  useEffect(() => {
    chordVoicingRef.current = chordOffsets(chordState?.keys ?? null, pitchHz);
  }, [chordState, pitchHz]);
  useEffect(() => {
    octaveKeysRef.current = octaveKeys;
  }, [octaveKeys]);
  useEffect(() => {
    bakedKeysRef.current = bakedKeys;
  }, [bakedKeys]);
  useEffect(() => {
    loopMelodyRef.current = loopMelody;
  }, [loopMelody]);
  useEffect(() => {
    melodyOnRef.current = melodyOn;
  }, [melodyOn]);
  useEffect(() => {
    isRandomPitchRef.current = isRandomPitch;
  }, [isRandomPitch]);

  // Spectrum colours for the bars. With a family selected, tint each firing bar by its
  // baked pitch (deterministic per pattern/seed) — this is the loop-on colouring and the
  // pre-play preview for loop-off (the live scheduler overwrites those per hit). Without a
  // family (the non-melody player), leave the refs null so the bars stay flat blue.
  useEffect(() => {
    if (!pattern) return;
    if (!melodyOn || !bakedKeys) {
      fillColorsRef.current = null;
      strokeColorsRef.current = null;
    } else {
      const n = pattern.pulses.length;
      const fills = Array<string>(n).fill(BLUE_FILL);
      const strokes = Array<string>(n).fill(BLUE_STROKE);
      const idx = firingPulseIndices(pattern.pulses);
      bakedKeys.forEach((key, e) => {
        fills[idx[e]] = pitchFill(key);
        strokes[idx[e]] = pitchStroke(key);
      });
      fillColorsRef.current = fills;
      strokeColorsRef.current = strokes;
    }
    // redraw(true, …) rebuilds the bar paths so the disp colour callbacks are re-read; a
    // bare redraw(false, …) would only repaint cached paths and ignore the new colours.
    // setScale=false keeps the fixed axis ranges. Handles family changes without a
    // regenerate (the plot is only recreated on a new pattern).
    plotRef.current?.redraw(true, false);
    // loopMelody is a dep so flipping loop back on mid-play restores the baked colours the
    // live loop-off scheduler had overwritten (the body always paints the baked preview).
  }, [pattern, melodyOn, octaveKeys, bakedKeys, loopMelody]);

  // --- Parameter editing (does NOT regenerate the pattern; only Generate does) ---

  const onMeterText = (text: string) => {
    setMeterText(text);
    try {
      const m = parseMeter(text);
      setMeter(m);
      setMeterError(null);
      // A new group count can invalidate a per-group subdivision list — re-check it.
      try {
        setSubdivisions(parseSubdivisions(subText, m.length));
        setSubError(null);
      } catch (e) {
        setSubError((e as Error).message);
      }
    } catch (e) {
      setMeterError((e as Error).message);
    }
  };

  const onSubText = (text: string) => {
    setSubText(text);
    try {
      setSubdivisions(parseSubdivisions(text, meter.length));
      setSubError(null);
    } catch (e) {
      setSubError((e as Error).message);
    }
  };

  // Chord entry (chord mode): find the typed chord's LCM ≤ 24 superset placements and make it
  // the sounding chord. A chord with no match is still valid — it sounds as accompaniment
  // (driven by chordState.keys) with the melody off. Only malformed input (bad keys / wrong
  // count) is an error, which keeps the last valid chord.
  const onChordText = (text: string) => {
    setChordText(text);
    try {
      const keys = parseChordKeys(text);
      setChordState({keys, matches: findSupersets(keys, 12, MAX_CHORD_LCM)});
      setSelectedMatchIdx(0);
      setChordError(null);
    } catch (e) {
      setChordError((e as Error).message);
    }
  };

  // Number-box entry for the two [0,1] sliders. Keep the raw text while editing so
  // decimals type smoothly; update the numeric value (clamped) whenever it parses.
  const onSyncText = (text: string) => {
    setSyncText(text);
    const v = parseFloat(text);
    if (!Number.isNaN(v)) setSyncopation(clampUnit(v));
  };
  const onResText = (text: string) => {
    setResText(text);
    const v = parseFloat(text);
    if (!Number.isNaN(v)) setResolution(clampUnit(v));
  };

  const hasError = meterError !== null || subError !== null;

  // --- Audio ---

  const getSynth = () => {
    if (!synthRef.current) {
      gainRef.current = new Tone.Gain(0.55).toDestination(); // master headroom
      synthRef.current = new Tone.Synth({
        oscillator: {type: 'triangle'},
        // Percussive blip with a short body: a small sustain lets the per-hit duration
        // (set from velocity below) actually change the blip length, not just its volume.
        envelope: {attack: 0.001, decay: 0.04, sustain: 0.3, release: 0.04},
      }).connect(gainRef.current);
    }
    return synthRef.current;
  };

  // Lazily build the chord synth: a soft sine PolySynth, mixed well below the melody blips
  // so they stay audible over it. A short release keeps each meter group's chord a distinct
  // long note (re-articulated per group) rather than washing into a continuous pad. Only
  // used in chord mode.
  const getChordSynth = () => {
    if (!chordSynthRef.current) {
      chordGainRef.current = new Tone.Gain(0.12).toDestination();
      chordSynthRef.current = new Tone.PolySynth(Tone.Synth, {
        oscillator: {type: 'sine'},
        envelope: {attack: 0.03, decay: 0.15, sustain: 0.7, release: 0.5},
      }).connect(chordGainRef.current);
    }
    return chordSynthRef.current;
  };

  // Lazily build the sampled-piano melody voice: a Tone.Sampler over the C-per-octave
  // samples, on its own persistent gain node (kept for the component's life). Same
  // triggerAttackRelease(freqHz, durSec, at, vel) surface as the sine synths, so the
  // scheduler treats it identically. Samples fetch on first build; the caller awaits load.
  const getPianoMelody = () => {
    if (!pianoMelodyRef.current) {
      pianoMelodyGainRef.current = new Tone.Gain(0.5).toDestination();
      pianoMelodyRef.current = new Tone.Sampler({
        urls: PIANO_URLS,
        baseUrl: sampleBase,
        release: 0.8,
      }).connect(pianoMelodyGainRef.current);
    }
    return pianoMelodyRef.current;
  };

  // Lazily build the sampled-piano chord voice — same Sampler, its own gain node mixed a
  // touch below the melody so the blips stay audible over the pad. Only used in chord mode.
  const getPianoChord = () => {
    if (!pianoChordRef.current) {
      pianoChordGainRef.current = new Tone.Gain(0.4).toDestination();
      pianoChordRef.current = new Tone.Sampler({
        urls: PIANO_URLS,
        baseUrl: sampleBase,
        release: 0.8,
      }).connect(pianoChordGainRef.current);
    }
    return pianoChordRef.current;
  };

  const stop = () => {
    if (loopTimerRef.current) {
      clearInterval(loopTimerRef.current);
      loopTimerRef.current = null;
    }
    synthRef.current?.dispose(); // cancels future scheduled clicks + cuts the voice
    synthRef.current = null; // a disposed synth can't retrigger; rebuild next play
    gainRef.current?.dispose();
    gainRef.current = null;
    chordSynthRef.current?.dispose(); // cut the sounding chord too
    chordSynthRef.current = null;
    chordGainRef.current?.dispose();
    chordGainRef.current = null;
    // Piano voices are cached across Stop/Play (see builders); just cut any ringing notes
    // rather than disposing, so the next Play reuses the loaded samples without re-fetching.
    pianoMelodyRef.current?.releaseAll();
    pianoChordRef.current?.releaseAll();
    // Invalidate in-flight Draw callbacks (up to the ~0.3 s look-ahead) and hide the marker.
    playRunRef.current++;
    playheadBeatRef.current = null;
    plotRef.current?.redraw(false, false);
    setPlaying(false);
  };

  useEffect(
    () => () => {
      stop();
      // Piano voices survive Stop; tear them (and their gains) down for good on unmount.
      pianoMelodyRef.current?.dispose();
      pianoMelodyGainRef.current?.dispose();
      pianoChordRef.current?.dispose();
      pianoChordGainRef.current?.dispose();
    },
    [],
  ); // dispose on unmount

  const play = async () => {
    if (!pattern) return;
    await Tone.start(); // unlock audio on the user gesture
    // Pick the active voices from the current instrument. Piano builds its samplers on first
    // use and fetches samples; gate playback on Tone.loaded() so the first note isn't silent.
    const usePiano = instrument === 'piano';
    if (usePiano) {
      getPianoMelody();
      if (chord) getPianoChord();
      setPianoLoading(true);
      try {
        await Tone.loaded();
      } finally {
        setPianoLoading(false);
      }
    }
    const synth = usePiano ? getPianoMelody() : getSynth();
    const chordSynth = chord ? (usePiano ? getPianoChord() : getChordSynth()) : null;
    const {pulses, totalBeats, meter: patternMeter} = pattern;
    // Chord onsets: map each meter group-start unit beat → that group's length in unit
    // beats (cumulative sums over the meter, as in gridLines' groupStarts). The chord
    // re-strikes at each group start, held for its group's length. Group starts are integer
    // on-beats that always fire, so exact key lookup against a firing event's unitBeat is
    // safe and every group start is reached by the events/i iteration below.
    const groupBeatsByStart = new Map<number, number>();
    let groupAcc = 0;
    for (const m of patternMeter) {
      groupBeatsByStart.set(groupAcc, m);
      groupAcc += m;
    }
    // Only firing pulses become onsets, sorted by position within the cycle.
    const events = firingEvents(pulses);
    // Bar index of each onset, aligned with `events`, for live loop-off recolouring.
    const firingPulseIdx = firingPulseIndices(pulses);
    const N = events.length;
    if (N === 0) return;
    const runId = ++playRunRef.current; // tags this run; stale Draw callbacks no-op
    setPlaying(true);

    // Continuous look-ahead loop copied from SequencePlayerClient: schedule each onset
    // a little ahead of the audio clock, anchored on the last onset actually queued, so
    // a live tempo change stretches the rhythm within the cycle instead of at its edge.
    const lookAheadSec = 0.3;
    const t0 = Tone.now() + 0.06;
    let i = 0;
    let prevBeat = 0;
    let prevTime = t0;
    const pump = () => {
      const sec = tempoRef.current; // seconds per unit beat, read live
      for (;;) {
        const ev = events[i % N];
        const absBeat = Math.floor(i / N) * totalBeats + ev.unitBeat;
        const at = prevTime + (absBeat - prevBeat) * sec;
        if (at >= Tone.now() + lookAheadSec) break; // not due yet — recompute next poll
        // Chord: re-strike the whole voicing at each meter group start, held for that
        // group's length (in live-tempo seconds) so it tracks tempo changes — a slow
        // harmonic pulse beneath the faster melody. Offsets are the precomputed
        // lowest-penalty voicing, rooted an octave below the melody (see chordVoicingRef);
        // map them to frequencies via the 12-TET ratio.
        const groupBeats = groupBeatsByStart.get(ev.unitBeat);
        if (chordSynth && groupBeats !== undefined) {
          const offsets = chordVoicingRef.current;
          if (offsets && offsets.length > 0) {
            const freqs = offsets.map((off) => pitchHz * Math.pow(2, off / 12));
            // Hold for most of the group but stop short of the next group start, leaving a
            // brief gap (like the melody's short blips) so each re-strike articulates
            // instead of butting up against the next and sounding continuous.
            chordSynth.triggerAttackRelease(freqs, groupBeats * sec * 0.95, at);
          }
        }
        const vel = ev.velocity / 127;
        const durSec = 0.03 + 0.12 * vel; // heavier accents are both louder and longer
        // Pitch: the fixed pitchHz unless a melody is active, in which case draw a key —
        // baked (a repeating phrase) or fresh per hit — placed above the root pitchHz
        // (key 0) via the 12-TET ratio 2^(key/12). A family draws a discrete key from its
        // octave pool; random pitch draws a continuous key in [0,12).
        const okeys = octaveKeysRef.current;
        let freq = pitchHz;
        // Loop-off re-rolls each hit; capture that key so the Draw callback can light up this
        // bar's spectrum colour when it sounds (loop-on / non-melody leave colourKey null).
        let colourKey: number | null = null;
        if (melodyOnRef.current) {
          const baked = bakedKeysRef.current;
          const loopOn = loopMelodyRef.current && baked;
          const key = loopOn
            ? baked![i % N]
            : isRandomPitchRef.current
              ? Math.random() * 12
              : okeys![Math.floor(Math.random() * okeys!.length)];
          freq = pitchHz * Math.pow(2, key / 12);
          if (!loopOn) colourKey = key;
        }
        // One Draw callback per onset, fired exactly when it sounds: move the sing-along
        // playhead to this bar (all players) and, for loop-off melody, recolour it live.
        const bar = firingPulseIdx[i % N];
        const beat = ev.unitBeat;
        Tone.getDraw().schedule(() => {
          if (playRunRef.current !== runId || !plotRef.current) return; // stale run / no plot
          playheadBeatRef.current = beat;
          if (colourKey != null) {
            (fillColorsRef.current ??= Array<string>(pulses.length).fill(BLUE_FILL))[bar] =
              pitchFill(colourKey);
            (strokeColorsRef.current ??= Array<string>(pulses.length).fill(BLUE_STROKE))[
              bar
            ] = pitchStroke(colourKey);
          }
          // Rebuild paths only when a colour changed (so disp re-reads); otherwise a cheap
          // repaint that still re-runs the playhead draw hook. setScale=false keeps the axes.
          plotRef.current.redraw(colourKey != null, false);
        }, at);
        synth.triggerAttackRelease(freq, durSec, at, vel);
        prevTime = at;
        prevBeat = absBeat;
        i++;
      }
    };
    pump();
    loopTimerRef.current = setInterval(pump, 80); // poll well inside the look-ahead
  };

  // --- Generation (the one place the concrete pattern changes) ---

  const regenerate = (nextSeed: number) => {
    const p = generatePattern(
      {meter, subdivisions, syncopation, resolution},
      nextSeed,
    );
    setPattern({...p, meter, subdivisions});
  };

  // Chord mode: roll a fresh chord + its superset matches, defaulting the melody to the
  // tightest match (fewest extra keys — matches are ranked tightest-first). The dropdown can
  // then switch among the rest to hear the ambiguity.
  const rollNewChord = () => {
    const {keys, matches} = rollChord(Math.random);
    setChordState({keys, matches});
    setSelectedMatchIdx(0);
    // Mirror the rolled chord into the editable box (and clear any stale error).
    setChordText(keys.join(' '));
    setChordError(null);
  };

  const generate = () => {
    stop();
    const nextSeed = (seed + 1) >>> 0;
    setSeed(nextSeed);
    regenerate(nextSeed);
  };

  // First mount: seed the visual so it isn't empty (uses the initial seed=1), and in chord
  // mode roll the first chord so the player has a melody family to draw from immediately.
  useEffect(() => {
    if (chord) rollNewChord();
    regenerate(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // --- Visualization: rebuild the bar plot whenever a new pattern is generated ---

  useEffect(() => {
    const container = containerRef.current;
    if (!container || !pattern) return;
    const {pulses, totalBeats, meter: pm, subdivisions: ps} = pattern;
    const xs = Float64Array.from(pulses, (p) => p.unitBeat);
    const ys = Float64Array.from(pulses, (p) => p.velocity);
    const lines = gridLines(pm, ps);
    const width = container.clientWidth || 700;

    const opts: uPlot.Options = {
      width,
      height,
      legend: {show: false},
      cursor: {show: false},
      scales: {
        x: {time: false, range: () => [-0.2, totalBeats + 0.2]},
        y: {range: () => [0, 127]},
      },
      axes: [
        {label: 'position (unit beats)', splits: () => lines.unitBeats},
        {label: 'velocity (0–127)', splits: () => [0, 32, 64, 96, 127]},
      ],
      series: [
        {},
        {
          label: 'velocity',
          stroke: BLUE_STROKE,
          fill: BLUE_FILL,
          // Per-bar spectrum colours via disp (unit 3 = Color): the values callbacks read
          // the live colour refs each draw, so a cheap redraw() repaints without a rebuild.
          // Null refs (the non-melody player) fall back to flat blue for every bar.
          paths: uPlot.paths.bars!({
            size: [0.55, 16],
            align: 0,
            disp: {
              fill: {
                unit: 3,
                values: (u) =>
                  fillColorsRef.current ??
                  Array<string>(u.data[0].length).fill(BLUE_FILL),
              },
              stroke: {
                unit: 3,
                values: (u) =>
                  strokeColorsRef.current ??
                  Array<string>(u.data[0].length).fill(BLUE_STROKE),
              },
            },
          }),
          points: {show: false},
        },
      ],
      plugins: [gridPlugin(lines), playheadPlugin(playheadBeatRef)],
    };

    const u = new uPlot(opts, [xs, ys] as uPlot.AlignedData, container);
    plotRef.current = u;

    const ro = new ResizeObserver(() => {
      u.setSize({width: container.clientWidth, height});
    });
    ro.observe(container);

    return () => {
      ro.disconnect();
      u.destroy();
      plotRef.current = null;
    };
  }, [pattern, height]);

  const maxSub = Math.max(1, ...subdivisions);
  const fastestMs = Math.round((60 / bpm / maxSub) * 1000);

  const rangeStyle = {width: '10rem', verticalAlign: 'middle'} as const;
  const labelStyle = {
    display: 'flex',
    alignItems: 'center',
    gap: '0.4rem',
  } as const;

  return (
    <div style={{margin: '1rem 0'}}>
      <div style={{position: 'relative'}}>
        <div ref={containerRef} style={{width: '100%', minHeight: height}} />
        {/* Spectrum legend — only when pitch colouring is active (a family or random pitch).
            The uPlot container mutates its own DOM, so this overlay is a sibling, not a child. */}
        {melodyOn && (
          <div
            style={{
              position: 'absolute',
              top: 10,
              right: 12,
              pointerEvents: 'none',
              display: 'flex',
              alignItems: 'center',
              gap: '0.35rem',
              fontSize: '0.7rem',
              color: 'var(--ifm-font-color-base)',
              background: 'var(--ifm-background-surface-color)',
              borderRadius: '4px',
              padding: '0.2rem 0.4rem',
              boxShadow: '0 1px 4px rgba(0,0,0,0.25)',
            }}>
            <span style={{opacity: 0.75}}>pitch</span>
            <span style={{opacity: 0.85}}>low</span>
            <span
              style={{
                display: 'inline-block',
                width: 72,
                height: 9,
                borderRadius: '3px',
                background: SPECTRUM_GRADIENT,
              }}
            />
            <span style={{opacity: 0.85}}>high</span>
          </div>
        )}
      </div>
      <div
        style={{
          fontSize: '0.8rem',
          opacity: 0.75,
          marginTop: '0.25rem',
          display: 'flex',
          gap: '1rem',
          flexWrap: 'wrap',
        }}>
        <span>
          <b>bold</b> = meter accent
        </span>
        <span>medium = unit beat</span>
        <span>faint = pulse</span>
        <span>
          · {bpm} BPM · fastest pulse {fastestMs} ms
        </span>
      </div>

      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '1rem 1.4rem',
          alignItems: 'center',
          marginTop: '0.7rem',
          fontSize: '0.9rem',
        }}>
        <label style={labelStyle}>
          meter
          <input
            type="text"
            value={meterText}
            onChange={(e) => onMeterText(e.target.value)}
            style={{width: '6rem'}}
            aria-label="meter groups"
          />
        </label>
        <label style={labelStyle}>
          subdivision
          <input
            type="text"
            value={subText}
            onChange={(e) => onSubText(e.target.value)}
            style={{width: '6rem'}}
            aria-label="subdivision per group"
          />
        </label>
        <label style={labelStyle}>
          tempo
          <input
            type="range"
            min={minBpm}
            max={maxBpm}
            step={1}
            value={bpm}
            onChange={(e) => setBpmBoth(parseFloat(e.target.value))}
            style={rangeStyle}
          />
          <input
            type="number"
            min={minBpm}
            max={maxBpm}
            value={bpm}
            onChange={(e) => {
              const v = parseFloat(e.target.value);
              if (!Number.isNaN(v)) setBpmBoth(v);
            }}
            style={{width: '4.5rem'}}
            aria-label="tempo in BPM"
          />
          <code>BPM</code>
        </label>
        <label style={labelStyle}>
          syncopation
          <input
            type="range"
            min={0}
            max={1}
            step={0.01}
            value={syncopation}
            onChange={(e) => {
              const v = clampUnit(parseFloat(e.target.value));
              setSyncopation(v);
              setSyncText(fmtUnit(v));
            }}
            style={rangeStyle}
          />
          <input
            type="text"
            inputMode="decimal"
            value={syncText}
            onChange={(e) => onSyncText(e.target.value)}
            onBlur={() => setSyncText(fmtUnit(syncopation))}
            style={{width: '4.5rem'}}
            aria-label="syncopation amount"
          />
        </label>
        <label style={labelStyle}>
          resolution
          <input
            type="range"
            min={0}
            max={1}
            step={0.01}
            value={resolution}
            onChange={(e) => {
              const v = clampUnit(parseFloat(e.target.value));
              setResolution(v);
              setResText(fmtUnit(v));
            }}
            style={rangeStyle}
          />
          <input
            type="text"
            inputMode="decimal"
            value={resText}
            onChange={(e) => onResText(e.target.value)}
            onBlur={() => setResText(fmtUnit(resolution))}
            style={{width: '4.5rem'}}
            aria-label="resolution amount"
          />
        </label>
        <label style={labelStyle}>
          instrument
          <select
            value={instrument}
            onChange={(e) => setInstrument(e.target.value as 'sine' | 'piano')}
            aria-label="instrument timbre">
            <option value="sine">Sine</option>
            <option value="piano">Piano</option>
          </select>
          {pianoLoading && (
            <span style={{opacity: 0.7, fontStyle: 'italic'}}>loading…</span>
          )}
        </label>
        {melody && !chord && (
          <label style={labelStyle}>
            lcm
            <select
              value={selectedLcm}
              onChange={(e) => setSelectedLcm(e.target.value)}
              aria-label="lcm family for melody pitches">
              {LCM_FAMILIES.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.label}
                </option>
              ))}
            </select>
          </label>
        )}
        {chord && chordState && (
          <>
            <label style={labelStyle}>
              chord
              <input
                type="text"
                value={chordText}
                onChange={(e) => onChordText(e.target.value)}
                style={{width: '7rem'}}
                aria-label="chord keys 0-11"
              />
            </label>
            {chordState.matches.length > 0 ? (
              <label style={labelStyle}>
                lcm
                <select
                  value={selectedMatchIdx}
                  onChange={(e) => setSelectedMatchIdx(Number(e.target.value))}
                  aria-label="matched lcm family for melody pitches">
                  {chordState.matches.map((m, i) => (
                    <option key={i} value={i}>
                      {matchLabel(m)}
                    </option>
                  ))}
                </select>
              </label>
            ) : (
              <span style={{...labelStyle, opacity: 0.7, fontStyle: 'italic'}}>
                no LCM ≤ 24 match
              </span>
            )}
          </>
        )}
        {(melody || chord) && (
          <label style={labelStyle}>
            <input
              type="checkbox"
              checked={loopMelody}
              onChange={(e) => setLoopMelody(e.target.checked)}
              aria-label="loop the drawn melody"
            />
            loop melody
          </label>
        )}
      </div>

      {(meterError || subError || chordError) && (
        <div
          style={{
            color: 'var(--ifm-color-danger)',
            fontSize: '0.85rem',
            marginTop: '0.4rem',
          }}>
          {meterError ?? subError ?? chordError}
        </div>
      )}

      <div style={{marginTop: '0.7rem', display: 'flex', gap: '0.6rem'}}>
        <button
          className="button button--primary button--sm"
          onClick={playing ? stop : play}
          disabled={pianoLoading}>
          {pianoLoading ? 'Loading…' : playing ? 'Stop' : 'Play'}
        </button>
        <button
          className="button button--secondary button--sm"
          onClick={generate}
          disabled={hasError}
          title={
            hasError
              ? 'Fix the meter / subdivision input first'
              : 'Stop and sample a new pattern'
          }>
          Generate Rhythm
        </button>
        {chord && (
          <button
            className="button button--secondary button--sm"
            onClick={rollNewChord}
            title="Roll a fresh random chord">
            Roll chord
          </button>
        )}
      </div>
    </div>
  );
}
