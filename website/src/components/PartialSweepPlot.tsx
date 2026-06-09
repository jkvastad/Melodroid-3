import React, {useEffect, useMemo, useRef} from 'react';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import {
  computeFreq,
  noteOffsets,
  partialAmp,
  partialCount,
  TimbreConfig,
} from '@site/src/lib/timbrePartials';

// A "sweep map" of an additive-synth spectrum: x is the swept slider parameter, y is partial
// frequency (log Hz), and each line is one partial of one note. Because the partials are
// computed from the same timbrePartials functions the audio uses, the picture is exact — you
// can watch overtones stretch, compress, and blend, and read collisions off where a track of
// one note crosses a track of another. A vertical cursor marks the current slider value.
//
// This is a plain client component (uPlot + DOM): it is only ever rendered inside
// TimbreMorphPlayerClient, which is already loaded client-only via BrowserOnly, so it needs no
// wrapper of its own.

export type PartialSweepPlotProps = {
  cfg: TimbreConfig;
  min: number; // axis-1 (slider) range
  max: number;
  slider: number; // current axis-1 value — drives the cursor
  step2: number; // current axis-2 (step size); only moves things in 'inharmonic-sweep'
  height?: number; // px, default 560
};

const SAMPLES = 200;

// Per-note hue palette (lower note first). Collisions read as a crossing of two different hues.
const NOTE_HUES = [210, 25, 140, 300, 50];
const noteColor = (n: number, alpha: number) =>
  `hsla(${NOTE_HUES[n % NOTE_HUES.length]}, 70%, 45%, ${alpha})`;
const noteSwatch = (n: number) => `hsl(${NOTE_HUES[n % NOTE_HUES.length]}, 70%, 45%)`;

const xAxisLabel = (cfg: TimbreConfig): string => {
  switch (cfg.mode) {
    case 'gamma':
      return 'stretch γ';
    case 'interval':
      return 'upper voice (semitones)';
    case 'inharmonic-sweep':
      return 'blend (harmonic → matched)';
    default:
      return 'blend (harmonic → inharmonic)';
  }
};

type Model = {
  data: (number[] | Float64Array)[];
  series: uPlot.Series[];
  yMin: number;
  yMax: number;
  noteCnt: number;
};

function buildModel(
  cfg: TimbreConfig,
  min: number,
  max: number,
  step2: number,
): Model {
  const pCount = partialCount(cfg);
  const noteCnt = noteOffsets(cfg, min, step2).length;

  const xs = new Float64Array(SAMPLES);
  for (let k = 0; k < SAMPLES; k++) {
    xs[k] = min + ((max - min) * k) / (SAMPLES - 1);
  }

  const data: (number[] | Float64Array)[] = [xs];
  const series: uPlot.Series[] = [{}];

  // Loud low partials read stronger (wider, more opaque) than the faint high ones.
  const ampRef = partialAmp(cfg, 0);
  let yMin = Infinity;
  let yMax = -Infinity;

  for (let n = 0; n < noteCnt; n++) {
    for (let i = 0; i < pCount; i++) {
      const y = new Float64Array(SAMPLES);
      for (let k = 0; k < SAMPLES; k++) {
        const f = computeFreq(cfg, n, i, xs[k], step2);
        y[k] = f;
        if (f < yMin) yMin = f;
        if (f > yMax) yMax = f;
      }
      data.push(y);
      const ampNorm = partialAmp(cfg, i) / ampRef; // (0, 1]
      series.push({
        label: `n${n} p${i + 1}`,
        stroke: noteColor(n, 0.35 + 0.6 * ampNorm),
        width: 1 + 1.6 * ampNorm,
      });
    }
  }

  return {data, series, yMin: yMin / 1.05, yMax: yMax * 1.05, noteCnt};
}

// Closest cross-note partial pair at the current slider values, in cents — the perceptual
// distance that turns into beating/roughness (or locks when ~0).
function closestCrossNoteCents(
  cfg: TimbreConfig,
  slider: number,
  step2: number,
): number | null {
  const pCount = partialCount(cfg);
  const noteCnt = noteOffsets(cfg, slider, step2).length;
  if (noteCnt < 2) return null;
  const freqs: number[][] = [];
  for (let n = 0; n < noteCnt; n++) {
    const row: number[] = [];
    for (let i = 0; i < pCount; i++) row.push(computeFreq(cfg, n, i, slider, step2));
    freqs.push(row);
  }
  let best = Infinity;
  for (let a = 0; a < noteCnt; a++) {
    for (let b = a + 1; b < noteCnt; b++) {
      for (const fa of freqs[a]) {
        for (const fb of freqs[b]) {
          const cents = Math.abs(1200 * Math.log2(fa / fb));
          if (cents < best) best = cents;
        }
      }
    }
  }
  return best;
}

// Draws the live slider cursor (a solid vertical line), reading the current value from a ref so
// dragging only needs a cheap u.redraw() — no rebuild. Mirrors WavePlotClient's vLinesPlugin.
function cursorPlugin(getX: () => number): uPlot.Plugin {
  return {
    hooks: {
      draw: (u) => {
        const {ctx} = u;
        const cx = Math.round(u.valToPos(getX(), 'x', true));
        ctx.save();
        ctx.strokeStyle = 'rgba(0,0,0,0.55)';
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.moveTo(cx, u.bbox.top);
        ctx.lineTo(cx, u.bbox.top + u.bbox.height);
        ctx.stroke();
        ctx.restore();
      },
    },
  };
}

export default function PartialSweepPlot({
  cfg,
  min,
  max,
  slider,
  step2,
  height = 560,
}: PartialSweepPlotProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const plotRef = useRef<uPlot | null>(null);
  const sliderRef = useRef(slider);
  sliderRef.current = slider;

  const cfgKey = JSON.stringify(cfg);
  const model = useMemo(
    () => buildModel(cfg, min, max, step2),
    [cfgKey, min, max, step2],
  );
  const modelRef = useRef(model);
  modelRef.current = model;

  // Recreate the plot only when its series structure / x-range changes — a step2 change keeps
  // the same series and just updates the data in place (below).
  const structureKey = useMemo(
    () => [cfgKey, min, max, height, model.noteCnt].join('|'),
    [cfgKey, min, max, height, model.noteCnt],
  );

  useEffect(() => {
    if (!containerRef.current) return;
    const m = modelRef.current;
    const width = containerRef.current.clientWidth || 700;
    const opts: uPlot.Options = {
      width,
      height,
      legend: {show: false},
      scales: {
        // These range fns run ONLY on autorange (initial load + double-click reset): a box-drag is
        // applied as an explicit setScale that bypasses them, so it still zooms. Returning the full
        // padded extent here keeps load/reset correct, and reading yMin/yMax from modelRef tracks a
        // step2 rebuild. (A pass-through `(_,lo,hi)=>[lo,hi]` would re-autorange y to the RAW,
        // unpadded extent on every redraw — the slider's redraw() sets x, which re-ranges auto y —
        // churning the series paths so they never draw until a double-click.)
        x: {time: false, range: () => [min, max]},
        y: {distr: 3, range: () => [modelRef.current.yMin, modelRef.current.yMax]},
      },
      axes: [
        {label: xAxisLabel(cfg)},
        {label: 'partial frequency (Hz)'},
      ],
      series: m.series,
      cursor: {drag: {x: true, y: true}},
      plugins: [cursorPlugin(() => sliderRef.current)],
    };

    const u = new uPlot(opts, m.data as uPlot.AlignedData, containerRef.current);
    plotRef.current = u;
    // No setScale here: construction autoranges both scales through the range fns above, landing on
    // the full padded extent — so the first paint is already correct.

    const ro = new ResizeObserver(() => {
      if (containerRef.current) {
        u.setSize({width: containerRef.current.clientWidth, height});
      }
    });
    ro.observe(containerRef.current);

    return () => {
      ro.disconnect();
      u.destroy();
      plotRef.current = null;
    };
  }, [structureKey]);

  // step2 change: same series, new y data. setData(_, true) repaints via the reset path, which
  // re-autoranges through the y range fn above — and that reads the now-current modelRef, so the
  // y view picks up the new step2 extent without an explicit setScale.
  useEffect(() => {
    const u = plotRef.current;
    if (!u) return;
    u.setData(model.data as uPlot.AlignedData, true);
  }, [model.data]);

  // slider change: just move the cursor line.
  useEffect(() => {
    plotRef.current?.redraw();
  }, [slider]);

  const cents = closestCrossNoteCents(cfg, slider, step2);
  const collisionNote =
    cents == null
      ? null
      : cents < 5
        ? `closest cross-note partials: ${Math.round(cents)}¢ — locking`
        : cents < 50
          ? `closest cross-note partials: ${Math.round(cents)}¢ — beating`
          : `closest cross-note partials: ${Math.round(cents)}¢ — clear`;

  return (
    <div style={{margin: '0.6rem 0'}}>
      <div ref={containerRef} style={{width: '100%'}} />
      <div style={{fontSize: '0.8rem', opacity: 0.7, marginTop: '0.25rem'}}>
        drag a box to zoom · double-click to reset
      </div>
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '1rem',
          alignItems: 'center',
          marginTop: '0.35rem',
          fontSize: '0.8rem',
          opacity: 0.85,
        }}>
        {Array.from({length: model.noteCnt}, (_, n) => (
          <span key={n} style={{display: 'flex', alignItems: 'center', gap: '0.35rem'}}>
            <span
              style={{
                display: 'inline-block',
                width: '0.9rem',
                height: '0.9rem',
                background: noteSwatch(n),
                borderRadius: 2,
              }}
            />
            {n === 0 ? 'lower note' : model.noteCnt === 2 ? 'upper note' : `note ${n + 1}`}
          </span>
        ))}
        {collisionNote && <span>· {collisionNote}</span>}
      </div>
    </div>
  );
}
