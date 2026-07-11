// Faithful TypeScript port of the C# placement math, so the docs can find a chord's LCM
// interpretations live in the browser instead of shelling out to the CLI. Mirrors
// src/Music/RatioMath.cs, src/Music/KeysNeeded.cs and src/Music/Placements.cs. The C# code
// stays the source of truth; verify parity against
// `melodroid table key-supersets --keys ... --ktet 12`.
//
// Reuses the fraction primitives already ported in lcmFamilies.ts (no math duplication).

import {fractionValue, lcmFamily, type Fraction} from './lcmFamilies';

// --- RatioMath.cs ----------------------------------------------------------

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

// --- Placements.cs ---------------------------------------------------------

// Map an already-fetched LCM family onto k-tet keys, anchored at `at`: each fraction's
// nearest key, shifted by `at`, folded into [0, k). Deduped + sorted. Mirrors
// Placements.Compute; findSupersets draws the melody from these keys.
function familyPlacementKeys(family: Fraction[], at: number, k: number): number[] {
  const keys = family.map(
    (f) => (((nearestKey(fractionValue(f), k).n + at) % k) + k) % k,
  );
  return [...new Set(keys)].sort((a, b) => a - b);
}

// One (lcm, at) placement whose k-tet keys contain the requested chord keys as a subset.
// `extra` is how many keys the placement adds beyond the chord — 0 means an exact key set.
export type Superset = {lcm: number; at: number; keys: number[]; extra: number};

// Enumerate every LCM family placement (over anchors 0..k-1) whose keys are a superset of the
// given chord keys, ranked fewest-extra-keys first (then by lcm, then anchor). Faithful port of
// Placements.FindSupersets; families are enumerated by scanning targetLcm 1..maxLcm and keeping
// the non-empty lcmFamily results, matching LcmFamilies.Compute. Unlike the old key-sweep this
// surfaces *every* interpretation of a chord (e.g. both 3@7 and 15@7 for `0 4 7`), not one
// folded LCM per reference key.
export function findSupersets(
  chordKeys: number[],
  k = 12,
  maxLcm = 24,
  maxSize = 24,
  maxPrime = 5,
): Superset[] {
  const requested = new Set(chordKeys.map((key) => ((key % k) + k) % k));
  const rows: Superset[] = [];
  for (let targetLcm = 1; targetLcm <= maxLcm; targetLcm++) {
    const family = lcmFamily(targetLcm, maxSize, maxPrime);
    if (family.length === 0) continue;
    for (let at = 0; at < k; at++) {
      const keys = familyPlacementKeys(family, at, k);
      const keySet = new Set(keys);
      if (![...requested].every((r) => keySet.has(r))) continue;
      rows.push({lcm: targetLcm, at, keys, extra: keySet.size - requested.size});
    }
  }
  return rows.sort((a, b) => a.extra - b.extra || a.lcm - b.lcm || a.at - b.at);
}
