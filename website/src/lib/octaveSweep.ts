// Faithful TypeScript port of the C# octave-sweep math, so the docs can bin a set of input
// ratios against the good fractions live in the browser instead of shipping the static CLI table.
// Mirrors src/Music/OctaveSweep.cs (and the RatioMath helpers it leans on). The C# code stays the
// source of truth; verify parity against `melodroid table octave-sweep --ratios 1.0 1.25 1.5`.
//
// Reuses the primitives already ported elsewhere (no math duplication): octaveNormalize and
// circularSignedRelative from placements.ts, goodFractions / lcm / fractionValue from lcmFamilies.ts.

import {
  fractionValue,
  goodFractions as enumerateGoodFractions,
  lcm,
  type Fraction,
} from './lcmFamilies';
import {circularSignedRelative, octaveNormalize} from './placements';

export type SweepMatch = {fraction: Fraction; signedPct: number};

export type SweepCell = {
  normalized: number;
  // The good fractions this input binned to, nearest first. Empty = a miss.
  matches: SweepMatch[];
  // ≥2 matches — the input sits in overlapping bins, so its fraction is undetermined.
  ambiguous: boolean;
};

export type SweepRow = {
  reference: number;
  cells: SweepCell[];
  // LCM of the matched denominators. null when any input missed (no determinate pattern length).
  // When ambiguous-but-no-miss this is the minimum reachable candidate LCM (like the C# `?` rows).
  lcm: number | null;
  lcmIsCandidate: boolean;
  // Every input fell in at least one bin.
  allBinned: boolean;
  // At least one input sat in overlapping bins.
  ambiguous: boolean;
  // Unambiguous AND every input binned — a clean full match.
  fullMatch: boolean;
};

// Convenience: the default good-fraction set, so callers don't re-enumerate per sweep step.
export function sweepGoodFractions(maxSize = 24, maxPrime = 5): Fraction[] {
  return enumerateGoodFractions(maxSize, maxPrime);
}

// Port of OctaveSweep.ComputeRow. For each input ratio, octave-normalise ratio/reference and bin
// it against every good fraction within binRadius (circular over [1, 2)); classify the row.
export function computeRow(
  reference: number,
  inputRatios: number[],
  goodFractions: Fraction[],
  binRadius: number,
): SweepRow {
  const cells: SweepCell[] = [];
  let rowAmbiguous = false;
  let allBinned = true;
  let noMisses = true;
  let foldedLcm = 1;
  let anyBinned = false;

  for (const ratio of inputRatios) {
    const normalized = octaveNormalize(ratio / reference);
    const matches: SweepMatch[] = [];
    for (const gf of goodFractions) {
      const signedRel = circularSignedRelative(normalized, fractionValue(gf));
      if (Math.abs(signedRel) <= binRadius) {
        matches.push({fraction: gf, signedPct: signedRel * 100.0});
      }
    }

    if (matches.length === 0) {
      cells.push({normalized, matches, ambiguous: false});
      allBinned = false;
      noMisses = false;
    } else if (matches.length === 1) {
      cells.push({normalized, matches, ambiguous: false});
      anyBinned = true;
      foldedLcm = lcm(foldedLcm, matches[0].fraction.denominator);
    } else {
      cells.push({normalized, matches, ambiguous: true});
      rowAmbiguous = true;
      allBinned = false;
    }
  }

  let rowLcm: number | null;
  let lcmIsCandidate = false;
  let fullMatch: boolean;
  if (rowAmbiguous) {
    fullMatch = false;
    if (noMisses) {
      rowLcm = minimumCandidateLcm(cells);
      lcmIsCandidate = true;
    } else {
      rowLcm = null;
    }
  } else if (allBinned) {
    rowLcm = foldedLcm;
    fullMatch = true;
  } else {
    rowLcm = anyBinned ? foldedLcm : null;
    fullMatch = false;
  }

  return {
    reference,
    cells,
    lcm: rowLcm,
    lcmIsCandidate,
    allBinned: noMisses,
    ambiguous: rowAmbiguous,
    fullMatch,
  };
}

// The smallest LCM reachable by picking one matched good fraction per cell (best wave-pattern
// length first). Assumes every cell has at least one match. Port of OctaveSweep.Enumerate.
function minimumCandidateLcm(cells: SweepCell[]): number {
  let best = Number.MAX_SAFE_INTEGER;
  const walk = (index: number, runningLcm: number): void => {
    if (runningLcm >= best) return;
    if (index === cells.length) {
      best = runningLcm;
      return;
    }
    for (const m of cells[index].matches) {
      walk(index + 1, lcm(runningLcm, m.fraction.denominator));
    }
  };
  walk(0, 1);
  return best;
}

// Port of OctaveSweep.Compute: sample references across [1, 2) at sweepStep.
export function computeSweep(
  inputRatios: number[],
  goodFractions: Fraction[],
  sweepStep: number,
  binRadius: number,
): SweepRow[] {
  const rows: SweepRow[] = [];
  if (inputRatios.length === 0 || goodFractions.length === 0) return rows;
  const stepCount = Math.ceil((2.0 - 1.0) / sweepStep);
  for (let i = 0; i < stepCount; i++) {
    const reference = 1.0 + i * sweepStep;
    if (reference >= 2.0) break;
    rows.push(computeRow(reference, inputRatios, goodFractions, binRadius));
  }
  return rows;
}

// Port of OctaveSweep.IdentifyCenteredFullMatches. A block is a maximal contiguous run of
// allBinned rows sharing the same per-cell nearest-fraction signature; each block contributes the
// one row minimising the worst-cell |signed %| distance (ties → lowest index). Returns those row
// indices (the ★ rows).
export function identifyCenteredFullMatches(rows: SweepRow[]): Set<number> {
  const centered = new Set<number>();
  let blockStart = -1;
  let bestIndex = -1;
  let bestMaxAbs = Number.POSITIVE_INFINITY;

  const signatureKey = (row: SweepRow): string =>
    row.cells
      .map((c) =>
        c.matches.length > 0
          ? `${c.matches[0].fraction.numerator}/${c.matches[0].fraction.denominator}`
          : '-',
      )
      .join('|');

  const maxAbsDistance = (row: SweepRow): number =>
    row.cells.reduce(
      (max, c) =>
        c.matches.length > 0 ? Math.max(max, Math.abs(c.matches[0].signedPct)) : max,
      0,
    );

  for (let i = 0; i < rows.length; i++) {
    const row = rows[i];
    if (!row.allBinned) {
      if (blockStart >= 0) {
        centered.add(bestIndex);
        blockStart = -1;
      }
      continue;
    }

    if (blockStart >= 0 && signatureKey(rows[blockStart]) !== signatureKey(row)) {
      centered.add(bestIndex);
      blockStart = -1;
    }

    if (blockStart < 0) {
      blockStart = i;
      bestIndex = i;
      bestMaxAbs = maxAbsDistance(row);
    } else {
      const maxAbs = maxAbsDistance(row);
      if (maxAbs < bestMaxAbs) {
        bestMaxAbs = maxAbs;
        bestIndex = i;
      }
    }
  }

  if (blockStart >= 0) centered.add(bestIndex);
  return centered;
}
