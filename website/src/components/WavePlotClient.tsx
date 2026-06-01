import React, {useEffect, useMemo, useRef, useState} from 'react';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import {
  fractionLabel,
  fractionValue,
  lcmFamily,
  sampleSine,
} from '@site/src/lib/lcmFamilies';

export type WavePlotProps = {
  lcm: number;
  maxSize?: number; // default 24
  maxPrime?: number; // default 5
  samplesPerPeriod?: number; // default 200
  mode?: 'all' | 'sum' | 'constituents' | 'difference'; // default 'all'
  subsetLcm?: number;
  differenceOnly?: boolean;
  height?: number; // px, default 360
};

type VLine = {x: number; color: string};

// Distinct hues for the constituent sines (ScottPlot cycles a palette; we just
// want them visually separable, not pixel-identical).
const constituentColor = (i: number, n: number): string =>
  `hsl(${Math.round((360 * i) / Math.max(1, n))}, 70%, 45%)`;

// Draw plugin for the dashed vertical reference lines (LCM endpoint in gray,
// sub-family period markers in translucent blue) — mirrors the VerticalLine
// calls in LcmFamilyWaveformRenderer.cs.
function vLinesPlugin(getLines: () => VLine[]): uPlot.Plugin {
  return {
    hooks: {
      draw: (u) => {
        const {ctx} = u;
        const lines = getLines();
        ctx.save();
        ctx.setLineDash([5, 4]);
        ctx.lineWidth = 1;
        for (const {x, color} of lines) {
          const cx = Math.round(u.valToPos(x, 'x', true));
          ctx.strokeStyle = color;
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

type PlotModel = {
  data: (number[] | Float64Array)[];
  series: uPlot.Series[];
  yMin: number;
  yMax: number;
  vlines: VLine[];
  title: string;
  error?: string;
};

function buildModel(
  lcmValue: number,
  maxSize: number,
  maxPrime: number,
  samplesPerPeriod: number,
  mode: NonNullable<WavePlotProps['mode']>,
  subsetLcm: number | undefined,
  differenceOnly: boolean,
): PlotModel {
  const empty: PlotModel = {
    data: [[]],
    series: [{}],
    yMin: -1,
    yMax: 1,
    vlines: [],
    title: `LCM family L=${lcmValue}`,
  };

  const family = lcmFamily(lcmValue, maxSize, maxPrime);
  if (family.length === 0) {
    return {
      ...empty,
      error: `No LCM family at L=${lcmValue} for max-size ${maxSize}, max-prime ${maxPrime}.`,
    };
  }

  const n = family.length;
  const len = samplesPerPeriod * lcmValue + 1;

  // Shared time axis (reference periods) + element-wise superposition.
  let t: Float64Array | null = null;
  const sum = new Float64Array(len);
  const constituentYs: Float64Array[] = [];
  for (const f of family) {
    const {t: ts, y} = sampleSine(fractionValue(f), lcmValue, samplesPerPeriod);
    t ??= ts;
    for (let i = 0; i < y.length; i++) sum[i] += y[i];
    constituentYs.push(y);
  }
  const xs = t!;

  const data: (number[] | Float64Array)[] = [xs];
  const series: uPlot.Series[] = [{label: 't'}];
  const vlines: VLine[] = [{x: lcmValue, color: 'rgba(128,128,128,0.9)'}];

  const showConstituents = mode === 'all' || mode === 'constituents';
  const showSum =
    mode !== 'constituents' && !(mode === 'difference' && differenceOnly);

  if (showConstituents) {
    family.forEach((f, i) => {
      data.push(constituentYs[i]);
      series.push({
        label: fractionLabel(f),
        stroke: constituentColor(i, n),
        width: 1.5,
      });
    });
  }

  if (showSum) {
    data.push(sum);
    series.push({label: 'sum', stroke: '#000000', width: 2.5});
  }

  // Sub-family overlay (literal subset whose LCM divides the parent's).
  if (subsetLcm !== undefined && lcmValue % subsetLcm === 0) {
    const sub = lcmFamily(subsetLcm, maxSize, maxPrime);
    if (sub.length > 0) {
      const iterations = lcmValue / subsetLcm;
      for (let i = 1; i < iterations; i++) {
        vlines.push({x: i * subsetLcm, color: 'rgba(0,0,255,0.4)'});
      }

      const subSum = new Float64Array(len);
      for (const f of sub) {
        const {y} = sampleSine(fractionValue(f), lcmValue, samplesPerPeriod);
        for (let i = 0; i < y.length; i++) subSum[i] += y[i];
      }

      if (!(mode === 'difference' && differenceOnly)) {
        data.push(subSum);
        series.push({
          label: `sum L=${subsetLcm} (${iterations}×)`,
          stroke: '#0000ff',
          width: 2,
        });
      }

      if (mode === 'difference') {
        const diff = new Float64Array(len);
        for (let i = 0; i < len; i++) diff[i] = sum[i] - subSum[i];
        data.push(diff);
        series.push({
          label: `L=${lcmValue} − sub L=${subsetLcm}`,
          stroke: '#ff0000',
          width: 2.5,
        });
      }
    }
  }

  // Mirror the title format of LcmFamilyWaveformRenderer.cs.
  const modeSuffix =
    mode === 'sum'
      ? ' — sum'
      : mode === 'constituents'
        ? ' — constituents'
        : mode === 'difference'
          ? differenceOnly
            ? ' — difference (only)'
            : ' — difference'
          : '';
  const subSuffix = subsetLcm === undefined ? '' : ` + sub L=${subsetLcm}`;
  const title = `LCM family L=${lcmValue} (${n} fractions)${modeSuffix}${subSuffix}`;

  return {data, series, yMin: -(n + 1), yMax: n + 1, vlines, title};
}

export default function WavePlotClient({
  lcm,
  maxSize = 24,
  maxPrime = 5,
  samplesPerPeriod: initialSpp = 200,
  mode: initialMode = 'all',
  subsetLcm,
  differenceOnly = false,
  height = 360,
}: WavePlotProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const plotRef = useRef<uPlot | null>(null);
  const vlinesRef = useRef<VLine[]>([]);

  const [mode, setMode] = useState<NonNullable<WavePlotProps['mode']>>(
    initialMode,
  );
  const [spp, setSpp] = useState(initialSpp);

  const model = useMemo(
    () =>
      buildModel(
        lcm,
        maxSize,
        maxPrime,
        spp,
        mode,
        subsetLcm,
        differenceOnly,
      ),
    [lcm, maxSize, maxPrime, spp, mode, subsetLcm, differenceOnly],
  );

  vlinesRef.current = model.vlines;

  // Latest model, read by the creation effect so it can build with up-to-date
  // data without re-running (and tearing down the plot) on every spp change.
  const modelRef = useRef(model);
  modelRef.current = model;

  // The plot's structure (series set, title, y-bounds, size) depends on these,
  // but not on spp. Re-creating uPlot only when this key changes lets a pure
  // spp change update data in place and keep any drag-zoom.
  const structureKey = useMemo(
    () => [lcm, maxSize, maxPrime, mode, subsetLcm, differenceOnly, height].join('|'),
    [lcm, maxSize, maxPrime, mode, subsetLcm, differenceOnly, height],
  );

  useEffect(() => {
    const model = modelRef.current;
    if (model.error || !containerRef.current) return;

    const width = containerRef.current.clientWidth || 700;
    const opts: uPlot.Options = {
      title: model.title,
      width,
      height,
      scales: {
        // Pass-through range: data extent ([0, lcm], tight) on init/reset, but
        // honors the zoomed bounds on drag-release (a static array would snap
        // back, defeating drag-to-zoom).
        x: {time: false, range: (_u, min, max) => [min, max]},
        y: {range: [model.yMin, model.yMax]},
      },
      axes: [
        {label: 't (reference periods)'},
        {label: 'amplitude'},
      ],
      series: model.series,
      cursor: {drag: {x: true, y: false}},
      plugins: [vLinesPlugin(() => vlinesRef.current)],
    };

    const u = new uPlot(opts, model.data as uPlot.AlignedData, containerRef.current);
    plotRef.current = u;

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

  // Re-sampling (spp change) only changes the data point count, not the series
  // structure — push it into the existing instance with resetScales=false so a
  // drag-zoom survives. setData also triggers a redraw, repainting the vlines.
  useEffect(() => {
    const u = plotRef.current;
    if (!u || model.error) return;
    u.setData(model.data as uPlot.AlignedData, false);
  }, [model.data, model.error]);

  if (model.error) {
    return (
      <p style={{color: 'var(--ifm-color-danger)', fontStyle: 'italic'}}>
        {model.error}
      </p>
    );
  }

  const modeOptions: NonNullable<WavePlotProps['mode']>[] =
    subsetLcm !== undefined
      ? ['all', 'sum', 'constituents', 'difference']
      : ['all', 'sum', 'constituents'];

  return (
    <div style={{margin: '1rem 0'}}>
      <div ref={containerRef} style={{width: '100%'}} />
      <div style={{fontSize: '0.8rem', opacity: 0.7, marginTop: '0.25rem'}}>
        drag to zoom · double-click to reset
      </div>
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '1rem',
          alignItems: 'center',
          marginTop: '0.5rem',
          fontSize: '0.85rem',
        }}>
        <label>
          mode:{' '}
          <select
            value={mode}
            onChange={(e) =>
              setMode(e.target.value as NonNullable<WavePlotProps['mode']>)
            }>
            {modeOptions.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
        </label>
        <label style={{display: 'flex', alignItems: 'center', gap: '0.4rem'}}>
          samples/period: {spp}
          <input
            type="range"
            min={20}
            max={400}
            step={20}
            value={spp}
            onChange={(e) => setSpp(Number(e.target.value))}
          />
        </label>
      </div>
    </div>
  );
}
