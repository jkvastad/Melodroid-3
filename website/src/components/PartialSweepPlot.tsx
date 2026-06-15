import React, {useEffect, useMemo, useRef} from 'react';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import {
  computeFreq,
  lockInterval,
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
  marks?: number[]; // x-positions (semitones) to mark with faint lines, e.g. slendro scale steps
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
    case 'stretch-interval':
      return 'interval (semitones)';
    case 'mixed-interval':
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
    const pCount = partialCount(cfg, n); // per-note: 'mixed-interval' voices can differ
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
  const noteCnt = noteOffsets(cfg, slider, step2).length;
  if (noteCnt < 2) return null;
  const freqs: number[][] = [];
  for (let n = 0; n < noteCnt; n++) {
    const row: number[] = [];
    for (let i = 0; i < partialCount(cfg, n); i++)
      row.push(computeFreq(cfg, n, i, slider, step2));
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

// Draws the static partial-lock interval (a red dashed vertical line) — the reader's target for
// highest consonance. Reads cfg/step2 from refs so it tracks the stretch slider on a bare redraw.
function lockLinePlugin(getCfg: () => TimbreConfig, getStep2: () => number): uPlot.Plugin {
  return {
    hooks: {
      draw: (u) => {
        const lockX = lockInterval(getCfg(), getStep2());
        if (lockX == null) return;
        const {min, max} = u.scales.x;
        if (min == null || max == null || lockX < min || lockX > max) return;
        const {ctx} = u;
        const cx = Math.round(u.valToPos(lockX, 'x', true));
        ctx.save();
        ctx.strokeStyle = 'rgba(214,40,40,0.9)';
        ctx.lineWidth = 1.5;
        ctx.setLineDash([5, 4]);
        ctx.beginPath();
        ctx.moveTo(cx, u.bbox.top);
        ctx.lineTo(cx, u.bbox.top + u.bbox.height);
        ctx.stroke();
        ctx.restore();
      },
    },
  };
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

// Draws faint dashed vertical lines at fixed x-positions (e.g. slendro scale degrees) so the
// reader can see where marked intervals fall relative to the partial tracks. A neutral violet,
// distinct from the note hues (blue/orange) and the black live cursor; drawn before both so they
// sit behind. The marks are static config, captured once.
function scaleMarksPlugin(marks: number[]): uPlot.Plugin {
  return {
    hooks: {
      draw: (u) => {
        const {min, max} = u.scales.x;
        if (min == null || max == null) return;
        const {ctx} = u;
        ctx.save();
        ctx.strokeStyle = 'rgba(120,110,160,0.45)';
        ctx.lineWidth = 1;
        ctx.setLineDash([3, 4]);
        for (const m of marks) {
          if (m < min || m > max) continue;
          const cx = Math.round(u.valToPos(m, 'x', true));
          ctx.beginPath();
          ctx.moveTo(cx, u.bbox.top);
          ctx.lineTo(cx, u.bbox.top + u.bbox.height);
          ctx.stroke();
        }
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
  marks,
  height = 560,
}: PartialSweepPlotProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const plotRef = useRef<uPlot | null>(null);
  const sliderRef = useRef(slider);
  sliderRef.current = slider;
  // step2 drives the partial-lock line from inside the draw hook (not React state), so mirror it
  // into a ref the same way as the slider.
  const step2Ref = useRef(step2);
  step2Ref.current = step2;
  // Whether the user has an active box zoom. Box zoom in this plot is effectively y-only: the
  // constant x range fn (`() => [min,max]`) re-runs for the x series on every commit (uPlot
  // setScales, i==0 branch) and snaps x back to full, while an explicit y stays put. So zoom lives
  // in the y scale — tracked here, narrower-than-full-extent, via the setScale hook below.
  const yZoomedRef = useRef(false);

  const cfgKey = JSON.stringify(cfg);
  const model = useMemo(
    () => buildModel(cfg, min, max, step2),
    [cfgKey, min, max, step2],
  );
  const modelRef = useRef(model);
  modelRef.current = model;

  // Recreate the plot only when its series structure / x-range changes — a step2 change keeps
  // the same series and just updates the data in place (below). marksKey is folded in so changing
  // the scale marks rebuilds (the marks plugin captures them once at construction).
  const marksKey = (marks ?? []).join(',');
  const structureKey = useMemo(
    () => [cfgKey, min, max, height, model.noteCnt, marksKey].join('|'),
    [cfgKey, min, max, height, model.noteCnt, marksKey],
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
        // Both range fns return the full padded extent, ignoring their args, so load + double-click
        // reset land correctly and reading yMin/yMax from modelRef tracks a step2 rebuild. (A
        // pass-through `(_,lo,hi)=>[lo,hi]` would re-autorange y to the RAW, unpadded extent on every
        // redraw — the slider's redraw() sets x, which re-ranges auto y — churning the series paths
        // so they never draw until a double-click.)
        //
        // Consequence (see uPlot setScales): the x range fn re-runs for the x series on EVERY commit,
        // so a box-drag's x zoom is immediately snapped back to full — box zoom here is effectively
        // y-only. An explicit y, by contrast, is skipped by the independent-scales loop and persists.
        // That is why the step2 effect below tracks and restores the y scale, not x.
        x: {time: false, range: () => [min, max]},
        y: {distr: 3, range: () => [modelRef.current.yMin, modelRef.current.yMax]},
      },
      axes: [
        {label: xAxisLabel(cfg)},
        {label: 'partial frequency (Hz)'},
      ],
      series: m.series,
      cursor: {drag: {x: true, y: true}},
      plugins: [
        // scale marks first (behind), then the lock line, then the black live cursor on top where
        // they coincide (γ=2 → both at 12).
        scaleMarksPlugin(marks ?? []),
        lockLinePlugin(() => cfg, () => step2Ref.current),
        cursorPlugin(() => sliderRef.current),
      ],
      // Track y-zoom: a box-drag narrows y below the full [yMin,yMax] extent; a double-click reset
      // (or our own autorange below) restores it. modelRef holds the currently-displayed extent, so
      // this stays correct as step2 rebuilds change yMin/yMax. The step2 effect reads it to decide
      // whether to preserve the user's view.
      hooks: {
        setScale: [
          (u, key) => {
            if (key !== 'y') return;
            const {yMin, yMax} = modelRef.current;
            const sy = u.scales.y;
            const eps = (yMax - yMin) * 1e-6 || 1e-9;
            yZoomedRef.current =
              sy.min != null && sy.max != null && (sy.min > yMin + eps || sy.max < yMax - eps);
          },
        ],
      },
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

  // step2 change: same series, new partial data — repaint without discarding an active box zoom.
  // When the user has zoomed (y narrowed), load the new data silently with setData(_, false) (which
  // never repaints on its own), then a single setScale('y') back to the current zoom: that one
  // commit rebuilds the paths for the new step and keeps y pinned (an explicit y is not re-ranged —
  // uPlot setScales, independent-scales loop), while x is left untouched at its full extent. When
  // NOT zoomed, setData(_, true) autoranges y through its range fn so the view follows the new
  // step's partial spread (the original default).
  useEffect(() => {
    const u = plotRef.current;
    if (!u) return;
    if (yZoomedRef.current) {
      const yv = {min: u.scales.y.min!, max: u.scales.y.max!};
      u.setData(model.data as uPlot.AlignedData, false);
      u.setScale('y', yv);
    } else {
      u.setData(model.data as uPlot.AlignedData, true);
    }
  }, [model.data]);

  // slider change: just move the cursor line. redraw(false, false) repaints at the CURRENT
  // scales (commit path) so it re-fires the cursor plugin's draw hook without re-ranging — a
  // bare redraw() re-runs _setScale on x, which re-autoranges the auto y scale through its
  // range fn back to the full extent, throwing away any active box zoom.
  useEffect(() => {
    plotRef.current?.redraw(false, false);
  }, [slider]);

  const cents = closestCrossNoteCents(cfg, slider, step2);
  const hasLock = lockInterval(cfg, step2) != null;
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
        {hasLock && (
          <span style={{display: 'flex', alignItems: 'center', gap: '0.35rem'}}>
            <span
              style={{
                display: 'inline-block',
                width: '1.1rem',
                height: 0,
                borderTop: '2px dashed rgba(214,40,40,0.9)',
              }}
            />
            max-consonance interval
          </span>
        )}
        {marks && marks.length > 0 && (
          <span style={{display: 'flex', alignItems: 'center', gap: '0.35rem'}}>
            <span
              style={{
                display: 'inline-block',
                width: '1.1rem',
                height: 0,
                borderTop: '1px dashed rgba(120,110,160,0.8)',
              }}
            />
            scale steps
          </span>
        )}
        {collisionNote && <span>· {collisionNote}</span>}
      </div>
    </div>
  );
}
