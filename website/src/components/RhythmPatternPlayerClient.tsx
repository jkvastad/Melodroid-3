import React, {useEffect, useMemo, useRef, useState} from 'react';
import * as Tone from 'tone';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import {
  generatePattern,
  gridLines,
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
  pitchHz?: number; // fixed blip pitch in Hz; default 330
  height?: number; // plot height in px; default 240
};

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

export default function RhythmPatternPlayerClient({
  meter: meterProp = '4',
  subdivisions: subProp = '2',
  bpm: bpmProp = 100,
  minBpm = 40,
  maxBpm = 240,
  syncopation: syncProp = 0,
  resolution: resProp = 1,
  pitchHz = 330,
  height = 240,
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
  const [resolution, setResolution] = useState(resProp);
  const [seed, setSeed] = useState(1);
  const [pattern, setPattern] = useState<RenderedPattern | null>(null);
  const [playing, setPlaying] = useState(false);

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
    const events = pulses
      .filter((p) => p.velocity > 0)
      .sort((a, b) => a.unitBeat - b.unitBeat);
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
        synth.triggerAttackRelease(pitchHz, durSec, at, vel);
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
        y: {range: () => [0, 132]},
      },
      axes: [
        {label: 'position (unit beats)', splits: () => lines.unitBeats},
        {label: 'velocity (0–127)', splits: () => [0, 32, 64, 96, 127]},
      ],
      series: [
        {},
        {
          label: 'velocity',
          stroke: 'rgba(30,90,168,0.95)',
          fill: 'rgba(30,90,168,0.55)',
          paths: uPlot.paths.bars!({size: [0.55, 16], align: 0}),
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
            onChange={(e) => setSyncopation(parseFloat(e.target.value))}
            style={rangeStyle}
          />
          <code>{syncopation.toFixed(2)}</code>
        </label>
        <label style={labelStyle}>
          resolution
          <input
            type="range"
            min={0}
            max={1}
            step={0.01}
            value={resolution}
            onChange={(e) => setResolution(parseFloat(e.target.value))}
            style={rangeStyle}
          />
          <code>{resolution.toFixed(2)}</code>
        </label>
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
