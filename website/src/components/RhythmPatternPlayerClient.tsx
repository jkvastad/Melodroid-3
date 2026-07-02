import React, {useEffect, useMemo, useRef, useState} from 'react';
import * as Tone from 'tone';
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
};

// The LCM families of the intro table on voicings-and-lcm-families.mdx, keyed to that
// table's rows. `keys` holds the raw table voicing (so the provenance is visible); the
// player folds them into a single octave before drawing pitches from them. The leading
// id '0' is not a family: it is the chromatic draw pool — all 12 pitch classes, a uniform
// random draw over the whole octave rather than a good-fraction subset.
type LcmFamily = {id: string; label: string; keys: number[]};
const LCM_FAMILIES: LcmFamily[] = [
  {id: '0', label: '0 · Chromatic', keys: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]},
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

  // Melody (only surfaced when `melody`): which intro-table LCM family to draw pitches
  // from ('' = fixed pitch, today's behavior), and whether the drawn pitches are baked
  // into a repeating phrase (loop on) or re-rolled on every hit (loop off).
  const [selectedLcm, setSelectedLcm] = useState(melody ? '8,9,10,12' : '');
  const [loopMelody, setLoopMelody] = useState(true);

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
  const loopTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const plotRef = useRef<uPlot | null>(null);

  // Per-bar spectrum colours, indexed by position in the current pattern's `pulses`
  // (null ⇒ fall back to flat blue). The uPlot bar series reads these via `disp`; the
  // baked-colour effect and the live loop-off scheduler write them and cheap-redraw.
  const fillColorsRef = useRef<string[] | null>(null);
  const strokeColorsRef = useRef<string[] | null>(null);

  // --- Melody: pitch pool + per-event pitch assignment ---

  // The selected family folded to one octave, or null for fixed pitch.
  const octaveKeys = useMemo(() => {
    const fam = LCM_FAMILIES.find((f) => f.id === selectedLcm);
    return fam ? foldOctave(fam.keys) : null;
  }, [selectedLcm]);

  // Loop-on melody: one random key per firing event, drawn with the same seeded RNG as
  // the rhythm so a given (pattern, family, seed) always yields the same phrase. Re-rolls
  // when the rhythm (pattern/seed) or the family changes; null when fixed pitch.
  const bakedKeys = useMemo(() => {
    if (!pattern || !octaveKeys) return null;
    const rng = mulberry32(seed);
    return firingEvents(pattern.pulses).map(
      () => octaveKeys[Math.floor(rng() * octaveKeys.length)],
    );
  }, [pattern, octaveKeys, seed]);

  // Mirror the melody config into refs so the look-ahead scheduler (play's pump) reads the
  // current values live, exactly like tempoRef — switching family / loop retunes a running
  // loop without a replay.
  const octaveKeysRef = useRef(octaveKeys);
  const bakedKeysRef = useRef(bakedKeys);
  const loopMelodyRef = useRef(loopMelody);
  useEffect(() => {
    octaveKeysRef.current = octaveKeys;
  }, [octaveKeys]);
  useEffect(() => {
    bakedKeysRef.current = bakedKeys;
  }, [bakedKeys]);
  useEffect(() => {
    loopMelodyRef.current = loopMelody;
  }, [loopMelody]);

  // Spectrum colours for the bars. With a family selected, tint each firing bar by its
  // baked pitch (deterministic per pattern/seed) — this is the loop-on colouring and the
  // pre-play preview for loop-off (the live scheduler overwrites those per hit). Without a
  // family (the non-melody player), leave the refs null so the bars stay flat blue.
  useEffect(() => {
    if (!pattern) return;
    if (!octaveKeys || !bakedKeys) {
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
  }, [pattern, octaveKeys, bakedKeys, loopMelody]);

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

  const stop = () => {
    if (loopTimerRef.current) {
      clearInterval(loopTimerRef.current);
      loopTimerRef.current = null;
    }
    synthRef.current?.dispose(); // cancels future scheduled clicks + cuts the voice
    synthRef.current = null; // a disposed synth can't retrigger; rebuild next play
    gainRef.current?.dispose();
    gainRef.current = null;
    setPlaying(false);
  };

  useEffect(() => () => stop(), []); // dispose on unmount

  const play = async () => {
    if (!pattern) return;
    await Tone.start(); // unlock audio on the user gesture
    const synth = getSynth();
    const {pulses, totalBeats} = pattern;
    // Only firing pulses become onsets, sorted by position within the cycle.
    const events = firingEvents(pulses);
    // Bar index of each onset, aligned with `events`, for live loop-off recolouring.
    const firingPulseIdx = firingPulseIndices(pulses);
    const N = events.length;
    if (N === 0) return;
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
        const vel = ev.velocity / 127;
        const durSec = 0.03 + 0.12 * vel; // heavier accents are both louder and longer
        // Pitch: the fixed pitchHz unless an lcm family is selected, in which case draw a
        // key from its octave — baked (a repeating phrase) or fresh per hit — placed above
        // the root pitchHz (key 0) via the 12-TET ratio 2^(key/12).
        const okeys = octaveKeysRef.current;
        let freq = pitchHz;
        if (okeys && okeys.length > 0) {
          const baked = bakedKeysRef.current;
          const loopOn = loopMelodyRef.current && baked;
          const key = loopOn
            ? baked![i % N]
            : okeys[Math.floor(Math.random() * okeys.length)];
          freq = pitchHz * Math.pow(2, key / 12);
          // Loop-off re-rolls each hit; light up this bar's spectrum colour exactly when
          // it sounds so the colours visibly change every cycle (loop-on is already baked).
          if (!loopOn) {
            const bar = firingPulseIdx[i % N];
            Tone.getDraw().schedule(() => {
              if (!plotRef.current) return;
              (fillColorsRef.current ??= Array<string>(pulses.length).fill(BLUE_FILL))[
                bar
              ] = pitchFill(key);
              (strokeColorsRef.current ??= Array<string>(pulses.length).fill(
                BLUE_STROKE,
              ))[bar] = pitchStroke(key);
              // rebuildPaths=true so disp re-reads the updated colour; setScale=false keeps
              // the axes fixed (see the baked-colour effect for the same reasoning).
              plotRef.current.redraw(true, false);
            }, at);
          }
        }
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

  const generate = () => {
    stop();
    const nextSeed = (seed + 1) >>> 0;
    setSeed(nextSeed);
    regenerate(nextSeed);
  };

  // First mount: seed the visual so it isn't empty (uses the initial seed=1).
  useEffect(() => {
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
      plugins: [gridPlugin(lines)],
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
      <div ref={containerRef} style={{width: '100%', minHeight: height}} />
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
        {melody && (
          <>
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
            <label style={labelStyle}>
              <input
                type="checkbox"
                checked={loopMelody}
                onChange={(e) => setLoopMelody(e.target.checked)}
                aria-label="loop the drawn melody"
              />
              loop melody
            </label>
          </>
        )}
      </div>

      {(meterError || subError) && (
        <div
          style={{
            color: 'var(--ifm-color-danger)',
            fontSize: '0.85rem',
            marginTop: '0.4rem',
          }}>
          {meterError ?? subError}
        </div>
      )}

      <div style={{marginTop: '0.7rem', display: 'flex', gap: '0.6rem'}}>
        <button
          className="button button--primary button--sm"
          onClick={playing ? stop : play}>
          {playing ? 'Stop' : 'Play'}
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
          Generate
        </button>
      </div>
    </div>
  );
}
