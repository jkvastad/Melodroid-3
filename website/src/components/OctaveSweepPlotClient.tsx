import React, {useEffect, useMemo, useRef, useState} from 'react';
import uPlot from 'uplot';
import 'uplot/dist/uPlot.min.css';
import {fractionLabel, fractionValue} from '@site/src/lib/lcmFamilies';
import {octaveNormalize} from '@site/src/lib/placements';
import {
  computeRow,
  computeSweep,
  identifyCenteredFullMatches,
  sweepGoodFractions,
  type SweepRow,
} from '@site/src/lib/octaveSweep';
import * as Tone from 'tone';

// Interactive companion to the static `melodroid table octave-sweep` output. The top plot is the
// octave [1, 2): good fractions sit as fixed bins (shaded bands of multiplicative radius = the bin
// radius) and the input ratios renormalise onto it as the reference slider moves — green when a
// ratio lands in a bin, grey when it misses. When every input bins, it is a full match (the LCM is
// the wave-pattern length). The strip below sweeps the reference across the whole octave and shades
// where full matches occur ("tuning a radio"), with the current reference as a draggable cursor.
//
// All octave-sweep math is the browser port in src/lib/octaveSweep.ts (itself a port of
// src/Music/OctaveSweep.cs). The C# CLI stays the source of truth.

export type OctaveSweepPlotProps = {
  ratios?: number[]; // input ratios; default the major triad [1, 5/4, 3/2]
  binRadius?: number; // default 1/161 — the tightest non-overlapping radius
  sweepStep?: number; // reference sampling for the strip; default 0.001
  maxSize?: number; // good-fraction size bound; default 24
  maxPrime?: number; // good-fraction prime bound; default 5
  height?: number; // main plot height in px; default 170
  playable?: boolean; // show a Tone.js play button for the renormalised set; default true
};

type Perspective = 'fixed' | 'reference';

const DEFAULT_RATIOS = [1, 5 / 4, 3 / 2];
const BIN_1_161 = 1 / 161;
const FUNDAMENTAL_HZ = 330;

const MISS = 'rgba(120,120,120,0.55)';
const MATCH = 'rgba(46,160,67,0.9)';
const AMBIGUOUS = 'rgba(219,138,0,0.95)';

type Preset = {label: string; ratios: number[]};

// Each good fraction's bin is |circularSignedRelative(x, g)| ≤ c over the cyclic octave, i.e. up to
// three sub-intervals of [1, 2): the direct band around `center`, plus the two octave-wrapped images
// (a band near 1 can also catch values near 2 and vice-versa). Returns the visible pieces.
function bandIntervals(center: number, c: number): [number, number][] {
  const raw: [number, number][] = [
    [center * (1 - c), center * (1 + c)],
    [(center * (1 - c)) / 2, (center * (1 + c)) / 2],
    [center * (2 - c), center * (2 + c)],
  ];
  const out: [number, number][] = [];
  for (const [lo, hi] of raw) {
    const a = Math.max(lo, 1);
    const b = Math.min(hi, 2);
    if (b > a) out.push([a, b]);
  }
  return out;
}

// Shaded good-fraction bins. In the fixed-octave perspective the bands sit at the fractions; in the
// reference perspective they shift by ×reference (octave-folded) while the markers stay put — same
// matches, opposite frame. Reads live state from refs so a reference change is a cheap redraw.
function binsPlugin(
  gfValues: number[],
  getC: () => number,
  getRef: () => number,
  getPerspective: () => Perspective,
): uPlot.Plugin {
  return {
    hooks: {
      draw: (u) => {
        const c = getC();
        const reference = getRef();
        const perspective = getPerspective();
        const {ctx} = u;
        const top = u.bbox.top;
        const h = u.bbox.height;
        ctx.save();
        for (const g of gfValues) {
          const center = perspective === 'reference' ? octaveNormalize(g * reference) : g;
          for (const [lo, hi] of bandIntervals(center, c)) {
            const x0 = u.valToPos(lo, 'x', true);
            const x1 = u.valToPos(hi, 'x', true);
            ctx.fillStyle = 'rgba(46,160,67,0.14)';
            ctx.fillRect(x0, top, Math.max(1, x1 - x0), h);
          }
        }
        ctx.restore();
      },
    },
  };
}

// The input ratios as vertical markers. Colour reads straight off computeRow for the current
// reference (recomputed here, cheap), so audio/visual/caption never drift.
function markersPlugin(
  gfValues: number[],
  gf: ReturnType<typeof sweepGoodFractions>,
  getRatios: () => number[],
  getC: () => number,
  getRef: () => number,
  getPerspective: () => Perspective,
): uPlot.Plugin {
  return {
    hooks: {
      draw: (u) => {
        const ratios = getRatios();
        const reference = getRef();
        const c = getC();
        const perspective = getPerspective();
        const row = computeRow(reference, ratios, gf, c);
        const {ctx} = u;
        const top = u.bbox.top;
        const h = u.bbox.height;
        ctx.save();
        ratios.forEach((ratio, i) => {
          const cell = row.cells[i];
          const x =
            perspective === 'reference'
              ? octaveNormalize(ratio)
              : octaveNormalize(ratio / reference);
          const color = cell.ambiguous
            ? AMBIGUOUS
            : cell.matches.length > 0
              ? MATCH
              : MISS;
          const cx = Math.round(u.valToPos(x, 'x', true));
          ctx.strokeStyle = color;
          ctx.lineWidth = 2;
          ctx.beginPath();
          ctx.moveTo(cx, top);
          ctx.lineTo(cx, top + h);
          ctx.stroke();
          ctx.fillStyle = color;
          ctx.beginPath();
          ctx.arc(cx, top + 7, 5, 0, 2 * Math.PI);
          ctx.fill();
        });
        ctx.restore();
      },
    },
  };
}

// Strip: shade each swept reference column where every input binned (full match = green, ambiguous
// full match = orange), with a brighter tick at the centered-full-match references.
function stripFillPlugin(
  getRows: () => SweepRow[],
  getCentered: () => Set<number>,
  getStep: () => number,
): uPlot.Plugin {
  return {
    hooks: {
      draw: (u) => {
        const rows = getRows();
        const centered = getCentered();
        const step = getStep();
        const {ctx} = u;
        const top = u.bbox.top;
        const h = u.bbox.height;
        ctx.save();
        rows.forEach((row, i) => {
          if (!row.allBinned) return;
          const x0 = u.valToPos(row.reference, 'x', true);
          const x1 = u.valToPos(Math.min(2, row.reference + step), 'x', true);
          ctx.fillStyle = row.fullMatch
            ? 'rgba(46,160,67,0.45)'
            : 'rgba(219,138,0,0.45)';
          ctx.fillRect(x0, top, Math.max(1, x1 - x0), h);
        });
        ctx.fillStyle = 'rgba(20,20,20,0.85)';
        centered.forEach((i) => {
          const row = rows[i];
          if (!row) return;
          const cx = Math.round(u.valToPos(row.reference, 'x', true));
          ctx.beginPath();
          ctx.moveTo(cx - 4, top);
          ctx.lineTo(cx + 4, top);
          ctx.lineTo(cx, top + 6);
          ctx.closePath();
          ctx.fill();
        });
        ctx.restore();
      },
    },
  };
}

// Solid vertical cursor at the current reference on the strip; live from a ref.
function cursorPlugin(getRef: () => number): uPlot.Plugin {
  return {
    hooks: {
      draw: (u) => {
        const {ctx} = u;
        const cx = Math.round(u.valToPos(getRef(), 'x', true));
        ctx.save();
        ctx.strokeStyle = 'rgba(0,0,0,0.65)';
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

export default function OctaveSweepPlotClient({
  ratios: ratiosProp,
  binRadius: binRadiusProp,
  sweepStep = 0.001,
  maxSize = 24,
  maxPrime = 5,
  height = 170,
  playable = true,
}: OctaveSweepPlotProps) {
  const gf = useMemo(() => sweepGoodFractions(maxSize, maxPrime), [maxSize, maxPrime]);
  const gfValues = useMemo(() => gf.map(fractionValue), [gf]);
  const gfLabelFor = useMemo(() => {
    const entries = gf.map((f) => ({v: fractionValue(f), label: fractionLabel(f)}));
    return (v: number) => {
      let best = entries[0];
      let bestD = Infinity;
      for (const e of entries) {
        const d = Math.abs(e.v - v);
        if (d < bestD) {
          bestD = d;
          best = e;
        }
      }
      return best.label;
    };
  }, [gf]);

  const presets: Preset[] = useMemo(
    () => [
      {label: 'Major {1, 5/4, 3/2}', ratios: [1, 5 / 4, 3 / 2]},
      {label: 'Minor {1, 6/5, 3/2}', ratios: [1, 6 / 5, 3 / 2]},
    ],
    [],
  );

  const [ratios, setRatios] = useState<number[]>(ratiosProp ?? DEFAULT_RATIOS);
  const [binRadius, setBinRadius] = useState<number>(binRadiusProp ?? BIN_1_161);
  const [reference, setReference] = useState<number>(1.0);
  const [perspective, setPerspective] = useState<Perspective>('reference');
  const [playing, setPlaying] = useState(false);
  const [customText, setCustomText] = useState('');
  const [customError, setCustomError] = useState<string | null>(null);

  // Live mirrors for the draw hooks (so a reference change is a bare redraw, never a rebuild).
  const ratiosRef = useRef(ratios);
  ratiosRef.current = ratios;
  const binRef = useRef(binRadius);
  binRef.current = binRadius;
  const refRef = useRef(reference);
  refRef.current = reference;
  const perspectiveRef = useRef(perspective);
  perspectiveRef.current = perspective;

  // Strip sweep data — recomputed only when the inputs/radius change.
  const sweep = useMemo(
    () => computeSweep(ratios, gf, sweepStep, binRadius),
    [ratios, gf, sweepStep, binRadius],
  );
  const centered = useMemo(() => identifyCenteredFullMatches(sweep), [sweep]);
  const sweepRef = useRef(sweep);
  sweepRef.current = sweep;
  const centeredRef = useRef(centered);
  centeredRef.current = centered;

  // Current-row readout for the caption — the same computeRow the markers use.
  const row = useMemo(
    () => computeRow(reference, ratios, gf, binRadius),
    [reference, ratios, gf, binRadius],
  );

  const mainContainer = useRef<HTMLDivElement | null>(null);
  const stripContainer = useRef<HTMLDivElement | null>(null);
  const mainPlot = useRef<uPlot | null>(null);
  const stripPlot = useRef<uPlot | null>(null);
  // Scrub target the strip's pointer handler writes to (stable across rebuilds).
  const scrubRef = useRef<(v: number) => void>(() => {});
  scrubRef.current = (v: number) => {
    setReference(Math.min(2 - sweepStep, Math.max(1, v)));
  };

  // --- main plot: rebuilt only on structural changes (perspective/radius/good-fraction set). ---
  useEffect(() => {
    if (!mainContainer.current) return;
    const width = mainContainer.current.clientWidth || 700;
    const opts: uPlot.Options = {
      width,
      height,
      legend: {show: false},
      cursor: {show: false},
      scales: {x: {distr: 3, range: () => [1, 2]}, y: {range: () => [0, 1]}},
      axes: [
        {
          splits: () => gfValues,
          values: (_u, splits) => splits.map((v) => gfLabelFor(v)),
          rotate: -60,
          size: 44,
        },
        {show: false},
      ],
      series: [{}, {show: false}],
      plugins: [
        binsPlugin(
          gfValues,
          () => binRef.current,
          () => refRef.current,
          () => perspectiveRef.current,
        ),
        markersPlugin(
          gfValues,
          gf,
          () => ratiosRef.current,
          () => binRef.current,
          () => refRef.current,
          () => perspectiveRef.current,
        ),
      ],
    };
    const u = new uPlot(opts, [[1, 2], [0, 0]] as uPlot.AlignedData, mainContainer.current);
    mainPlot.current = u;
    const ro = new ResizeObserver(() => {
      if (mainContainer.current) u.setSize({width: mainContainer.current.clientWidth, height});
    });
    ro.observe(mainContainer.current);
    return () => {
      ro.disconnect();
      u.destroy();
      mainPlot.current = null;
    };
  }, [gf, gfValues, gfLabelFor, height]);

  // --- strip plot: rebuilt on good-fraction set change; sweep data is read live from refs. ---
  useEffect(() => {
    if (!stripContainer.current) return;
    const stripH = 70;
    const width = stripContainer.current.clientWidth || 700;
    const opts: uPlot.Options = {
      width,
      height: stripH,
      legend: {show: false},
      cursor: {show: false},
      scales: {x: {range: () => [1, 2]}, y: {range: () => [0, 1]}},
      axes: [{}, {show: false}],
      series: [{}, {show: false}],
      plugins: [
        stripFillPlugin(
          () => sweepRef.current,
          () => centeredRef.current,
          () => sweepStep,
        ),
        cursorPlugin(() => refRef.current),
      ],
      hooks: {
        ready: [
          (u) => {
            const onPointer = (e: PointerEvent) => {
              const v = u.posToVal(e.offsetX, 'x');
              scrubRef.current(v);
            };
            u.over.addEventListener('pointerdown', (e) => {
              u.over.setPointerCapture(e.pointerId);
              onPointer(e);
            });
            u.over.addEventListener('pointermove', (e) => {
              if (e.buttons === 1) onPointer(e);
            });
            u.over.style.cursor = 'ew-resize';
          },
        ],
      },
    };
    const u = new uPlot(opts, [[1, 2], [0, 0]] as uPlot.AlignedData, stripContainer.current);
    stripPlot.current = u;
    const ro = new ResizeObserver(() => {
      if (stripContainer.current) u.setSize({width: stripContainer.current.clientWidth, height: stripH});
    });
    ro.observe(stripContainer.current);
    return () => {
      ro.disconnect();
      u.destroy();
      stripPlot.current = null;
    };
    // sweepStep is a prop constant; refs carry the live sweep/centered data.
  }, [gf, sweepStep]);

  // Reference change (slider / scrub / animation): cheap repaint of both plots, no rebuild.
  useEffect(() => {
    mainPlot.current?.redraw(false, false);
    stripPlot.current?.redraw(false, false);
  }, [reference, perspective, binRadius, ratios]);

  // New sweep data (inputs or radius changed): repaint the strip.
  useEffect(() => {
    stripPlot.current?.redraw(false, false);
  }, [sweep, centered]);

  // --- live audio: a sustained sine bank that tracks the reference ---
  // One oscillator per input ratio, held across reference changes; a reference move glides each
  // oscillator to its new renormalised frequency (no retrigger), so dragging the slider / scrubbing
  // the strip morphs the chord continuously — the consonance-chapter idiom from
  // TimbreMorphPlayerClient, one voice per ratio instead of per partial.
  const masterRef = useRef<Tone.Gain | null>(null);
  const oscBankRef = useRef<Array<{osc: Tone.Oscillator; gain: Tone.Gain}>>([]);

  // Heard pitch of input i: the renormalised, reference-relative ratio — the same set the markers
  // show — so it morphs with the reference regardless of the (visual-only) perspective.
  // Raw renormalised ratio — deliberately NOT octave-folded. Folding per voice makes a note leap up
  // an octave the instant the reference passes its ratio (the jumps at 1.001/1.251/1.501 for the
  // major triad). Dividing every voice by the same reference instead transposes the whole chord
  // together, so the relative intervals — hence chord quality and the full-match consonance being
  // auditioned — are identical, and the common octave just glides smoothly (≤1 octave, since
  // reference ∈ [1, 2)). The visual markers still wrap via octaveNormalize; only the audio glides.
  const freqFor = (i: number): number =>
    FUNDAMENTAL_HZ * (ratiosRef.current[i] / refRef.current);

  const buildBank = () => {
    const master = masterRef.current;
    if (!master) return;
    const n = ratiosRef.current.length;
    const level = n > 0 ? 0.3 / n : 0; // split headroom so N voices don't clip
    const t0 = Tone.now();
    const bank: Array<{osc: Tone.Oscillator; gain: Tone.Gain}> = [];
    for (let i = 0; i < n; i++) {
      const gain = new Tone.Gain(level).connect(master);
      const osc = new Tone.Oscillator(freqFor(i), 'sine').connect(gain);
      osc.start(t0);
      bank.push({osc, gain});
    }
    oscBankRef.current = bank;
  };

  const startAudio = async () => {
    await Tone.start(); // unlock audio on the user gesture
    const master = new Tone.Gain(0).toDestination();
    master.gain.rampTo(1, 0.02); // soft attack, no click
    masterRef.current = master;
    buildBank();
    setPlaying(true);
  };

  const stopAudio = () => {
    const master = masterRef.current;
    const bank = oscBankRef.current;
    masterRef.current = null;
    oscBankRef.current = [];
    setPlaying(false);
    if (!master) return;
    const fadeSec = 0.1;
    master.gain.rampTo(0, fadeSec); // fade sounding voices, no click
    setTimeout(() => {
      bank.forEach(({osc, gain}) => {
        osc.dispose(); // disposing (not just stopping) silences the free-running oscillator
        gain.dispose();
      });
      master.dispose();
    }, fadeSec * 1000 + 50);
  };

  useEffect(() => () => stopAudio(), []); // dispose on unmount

  // Reference change (slider / strip scrub) → glide every voice to its new frequency.
  // A 40 ms ramp kills zipper noise while staying responsive; it also smooths the octave-fold jump
  // when a ratio crosses out of [1, 2).
  useEffect(() => {
    if (!masterRef.current) return;
    oscBankRef.current.forEach(({osc}, i) => osc.frequency.rampTo(freqFor(i), 0.04));
  }, [reference]);

  // Input set change (preset / custom) while sounding → the voice count changed, so rebuild the
  // bank in place at the current reference, keeping the drone going.
  useEffect(() => {
    if (!masterRef.current) return;
    oscBankRef.current.forEach(({osc, gain}) => {
      osc.dispose();
      gain.dispose();
    });
    buildBank();
  }, [ratios]);

  // --- controls ---
  const applyCustom = () => {
    const parsed = customText
      .split(/[\s,]+/)
      .filter((t) => t.length > 0)
      .map(Number);
    if (parsed.length === 0 || parsed.some((n) => !isFinite(n) || n <= 0)) {
      setCustomError('Enter positive numbers, space- or comma-separated (e.g. 1 1.25 1.5).');
      return;
    }
    setCustomError(null);
    setReference(1.0);
    setRatios(parsed);
  };

  const ratiosEqual = (a: number[], b: number[]) =>
    a.length === b.length && a.every((v, i) => Math.abs(v - b[i]) < 1e-9);

  const matchLabel = row.fullMatch
    ? `full match — LCM ${row.lcm}`
    : row.ambiguous && row.allBinned
      ? `ambiguous full match — LCM ${row.lcm}? (overlapping bins)`
      : row.allBinned
        ? `LCM ${row.lcm}`
        : 'no full match';

  const btn = (active: boolean): React.CSSProperties => ({
    fontSize: '0.82rem',
    padding: '0.2rem 0.55rem',
    borderRadius: 4,
    border: '1px solid var(--ifm-color-emphasis-300)',
    background: active ? 'var(--ifm-color-primary)' : 'var(--ifm-background-surface-color)',
    color: active ? 'white' : 'inherit',
    cursor: 'pointer',
  });

  return (
    <div style={{margin: '0.8rem 0'}}>
      <div ref={mainContainer} style={{width: '100%'}} />
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '0.75rem',
          margin: '0.2rem 0 0.1rem',
          fontSize: '0.85rem',
        }}>
        <span style={{fontWeight: 600}}>
          reference {reference.toFixed(4)} ·{' '}
          <span style={{color: row.fullMatch ? 'var(--ifm-color-success)' : 'inherit'}}>
            {matchLabel}
          </span>
        </span>
      </div>

      <input
        type="range"
        min={1}
        max={2 - sweepStep}
        step={sweepStep}
        value={reference}
        onChange={(e) => setReference(Number(e.target.value))}
        style={{width: '100%'}}
        aria-label="reference ratio"
      />

      <div ref={stripContainer} style={{width: '100%', marginTop: '0.2rem'}} />
      <div style={{fontSize: '0.75rem', opacity: 0.65, marginTop: '0.15rem'}}>
        sweep strip — green: full match · orange: ambiguous full match · ▾: centered full match ·
        drag to scrub
      </div>

      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '0.75rem',
          alignItems: 'center',
          marginTop: '0.5rem',
        }}>
        <span style={{display: 'inline-flex', gap: '0.3rem'}}>
          <button
            style={btn(perspective === 'fixed')}
            onClick={() => setPerspective('fixed')}>
            fixed octave
          </button>
          <button
            style={btn(perspective === 'reference')}
            onClick={() => setPerspective('reference')}>
            reference
          </button>
        </span>

        <label style={{fontSize: '0.85rem', display: 'inline-flex', alignItems: 'center', gap: '0.35rem'}}>
          bin radius
          <input
            type="number"
            min={0}
            max={0.1}
            step={0.001}
            value={Number(binRadius.toFixed(5))}
            onChange={(e) => {
              const v = Number(e.target.value);
              if (isFinite(v) && v >= 0) setBinRadius(v);
            }}
            style={{width: '5rem'}}
          />
          <button style={btn(false)} onClick={() => setBinRadius(BIN_1_161)}>
            1/161
          </button>
        </label>

        {playable && (
          <button
            className="button button--primary button--sm"
            onClick={playing ? stopAudio : startAudio}>
            {playing ? 'Stop' : '▶ Hear set'}
          </button>
        )}
      </div>

      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          gap: '0.4rem',
          alignItems: 'center',
          marginTop: '0.5rem',
        }}>
        <span style={{fontSize: '0.85rem', opacity: 0.8}}>inputs:</span>
        {presets.map((p) => (
          <button
            key={p.label}
            style={btn(ratiosEqual(ratios, p.ratios))}
            onClick={() => {
              setReference(1.0);
              setRatios(p.ratios);
            }}>
            {p.label}
          </button>
        ))}
        <input
          type="text"
          placeholder="custom e.g. 1 1.25 1.5"
          value={customText}
          onChange={(e) => setCustomText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') applyCustom();
          }}
          style={{fontSize: '0.82rem', padding: '0.2rem 0.4rem', width: '11rem'}}
        />
        <button style={btn(false)} onClick={applyCustom}>
          apply
        </button>
      </div>
      {customError && (
        <div style={{color: 'var(--ifm-color-danger)', fontSize: '0.78rem', marginTop: '0.25rem'}}>
          {customError}
        </div>
      )}
    </div>
  );
}
