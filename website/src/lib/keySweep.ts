// Faithful TypeScript port of the C# key-sweep / placement math, so the docs can
// key-sweep a chord live in the browser instead of shelling out to the CLI. Mirrors
// src/Music/RatioMath.cs, src/Music/KeysNeeded.cs, src/Music/OctaveSweep.cs,
// src/Music/KeySweep.cs and src/Music/Placements.cs. The C# code stays the source of
// truth; verify parity against `melodroid table key-sweep --keys ... --ktet 12`.
//
// Reuses the fraction primitives already ported in lcmFamilies.ts (no math duplication).

import {
  fractionValue,
  goodFractions,
  lcm,
  lcmFamily,
  type Fraction,
} from './lcmFamilies';

// --- RatioMath.cs ----------------------------------------------------------

export function octaveNormalize(r: number): number {
  while (r < 1.0) r *= 2.0;
  while (r >= 2.0) r *= 0.5;
  return r;
}

// The octave [1, 2) is cyclic — 1.0 and 2.0 identify. For v, g both in [1, 2) pick the
// representative of v across the wrap closest to g, then return the signed relative
// offset to g ("v above g" → positive).
export function circularSignedRelative(v: number, g: number): number {
  const direct = (v - g) / g;
  const wrapUp = (2.0 * v - g) / g;
  const wrapDn = (v - 2.0 * g) / g;
  let best = direct;
  if (Math.abs(wrapUp) < Math.abs(best)) best = wrapUp;
  if (Math.abs(wrapDn) < Math.abs(best)) best = wrapDn;
  return best;
}

// --- KeysNeeded.cs ---------------------------------------------------------

export type NearestKey = {n: number; keyRatio: number; signedRelative: number};

// The k-tet key whose ratio 2^(n/k) sits closest (circularly) to a good fraction.
export function nearestKey(gValue: number, k: number): NearestKey {
  const idealN = k * Math.log2(gValue);
  const floorN = Math.floor(idealN);
  const ceilN = Math.ceil(idealN);

  const nFloor = ((floorN % k) + k) % k;
  const nCeil = ((ceilN % k) + k) % k;

  const ratioFloor = Math.pow(2.0, nFloor / k);
  const relFloor = circularSignedRelative(ratioFloor, gValue);

  if (nCeil === nFloor) {
    return {n: nFloor, keyRatio: ratioFloor, signedRelative: relFloor};
  }

  const ratioCeil = Math.pow(2.0, nCeil / k);
  const relCeil = circularSignedRelative(ratioCeil, gValue);

  if (Math.abs(relCeil) < Math.abs(relFloor)) {
    return {n: nCeil, keyRatio: ratioCeil, signedRelative: relCeil};
  }
  return {n: nFloor, keyRatio: ratioFloor, signedRelative: relFloor};
}

// The worst-case k-tet covering radius c_k: the largest distance from any good fraction
// to its nearest key. This is the CLI's default bin radius for key-sweep at this k
// (Program.cs: `KeysNeeded.WorstCaseForK(fractions, k).Radius`).
export function worstCaseRadius(fractions: Fraction[], k: number): number {
  let worst = -1.0;
  for (const g of fractions) {
    const dist = Math.abs(nearestKey(fractionValue(g), k).signedRelative);
    if (dist > worst) worst = dist;
  }
  return worst;
}

// --- OctaveSweep.cs / KeySweep.cs ------------------------------------------

export type FullMatch = {referenceKey: number; lcm: number};

// Bin one input ratio against the good fractions at the given radius, mirroring the
// per-cell logic of OctaveSweep.ComputeRow. Returns the matched denominators (0 misses,
// 1 unique bin, or >1 = ambiguous).
function binRatio(
  ratio: number,
  reference: number,
  fractions: Fraction[],
  binRadius: number,
): number[] {
  const normalized = octaveNormalize(ratio / reference);
  const dens: number[] = [];
  for (const gf of fractions) {
    const signedRel = circularSignedRelative(normalized, fractionValue(gf));
    if (Math.abs(signedRel) <= binRadius) dens.push(gf.denominator);
  }
  return dens;
}

// Key-sweep a chord (a set of k-tet key indices) against the good fractions. For each
// reference key n in 0..k-1 the chord is measured relative to 2^(n/k); a reference is a
// *full match* iff every chord note bins to exactly one good fraction (no misses, no
// ambiguities) — the OctaveSweep.ComputeRow `FullMatch = allBinned && !ambiguous` rule.
// The matched LCM is the LCM of the binned denominators. Several references (and several
// LCMs) may match: that ambiguity is the point.
export function keySweepChord(
  chordKeys: number[],
  k = 12,
  maxSize = 24,
  maxPrime = 5,
): FullMatch[] {
  const fractions = goodFractions(maxSize, maxPrime);
  const binRadius = worstCaseRadius(fractions, k);
  const ratios = chordKeys.map((key) => Math.pow(2.0, key / k));

  const matches: FullMatch[] = [];
  for (let n = 0; n < k; n++) {
    const reference = Math.pow(2.0, n / k);
    let foldedLcm = 1;
    let fullMatch = true;
    for (const ratio of ratios) {
      const dens = binRatio(ratio, reference, fractions, binRadius);
      if (dens.length !== 1) {
        fullMatch = false;
        break;
      }
      foldedLcm = lcm(foldedLcm, dens[0]);
    }
    if (fullMatch) matches.push({referenceKey: n, lcm: foldedLcm});
  }
  return matches;
}

// --- Placements.cs ---------------------------------------------------------

// Map an LCM family onto k-tet keys, anchored at `at`: each fraction's nearest key,
// shifted by `at`, folded into [0, k). Deduped + sorted (the caller draws melody from
// these). Mirrors Placements.Compute over lcmFamily(lcm).
export function placementKeys(
  lcmValue: number,
  at: number,
  k = 12,
  maxSize = 24,
  maxPrime = 5,
): number[] {
  const family = lcmFamily(lcmValue, maxSize, maxPrime);
  const keys = family.map((f) => {
    const k0 = nearestKey(fractionValue(f), k).n;
    return (((k0 + at) % k) + k) % k;
  });
  return [...new Set(keys)].sort((a, b) => a - b);
}
