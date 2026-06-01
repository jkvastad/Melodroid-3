// Faithful TypeScript port of the C# domain math used to plot LCM families.
// Mirrors src/Music/IntegerMath.cs, src/Music/GoodFractions.cs,
// src/Music/LcmFamilies.cs and src/Physics/Sine.cs so the docs can compute the
// same wave-pattern plots live in the browser instead of shipping static PNGs.

export type Fraction = {numerator: number; denominator: number};

export const fractionValue = (f: Fraction): number =>
  f.numerator / f.denominator;

export const fractionLabel = (f: Fraction): string =>
  `${f.numerator}/${f.denominator}`;

// --- IntegerMath.cs --------------------------------------------------------

export function gcd(a: number, b: number): number {
  a = Math.abs(a);
  b = Math.abs(b);
  while (b !== 0) [a, b] = [b, a % b];
  return a === 0 ? 1 : a;
}

export function lcm(a: number, b: number): number {
  return (a / gcd(a, b)) * b;
}

// --- GoodFractions.cs ------------------------------------------------------

function isPrime(n: number): boolean {
  if (n < 2) return false;
  for (let i = 2; i * i <= n; i++) if (n % i === 0) return false;
  return true;
}

function isSmooth(n: number, maxPrime: number): boolean {
  for (let prime = 2; prime <= maxPrime; prime++) {
    if (!isPrime(prime)) continue;
    while (n % prime === 0) n /= prime;
  }
  return n === 1;
}

export function goodFractions(maxSize: number, maxPrime: number): Fraction[] {
  const safeMaxSize = Math.max(1, maxSize);
  const safeMaxPrime = Math.max(2, maxPrime);

  const smooth: number[] = [];
  for (let n = 1; n <= safeMaxSize; n++) {
    if (isSmooth(n, safeMaxPrime)) smooth.push(n);
  }

  const result: Fraction[] = [];
  for (const q of smooth) {
    for (const p of smooth) {
      if (p < q || p >= 2 * q) continue;
      if (gcd(p, q) !== 1) continue;
      result.push({numerator: p, denominator: q});
    }
  }

  result.sort((a, b) => fractionValue(a) - fractionValue(b));
  return result;
}

// --- LcmFamilies.cs --------------------------------------------------------

// The maximal subset of good fractions whose denominators have the given LCM.
// Returns an empty array when no exact family exists at `targetLcm` under the
// current maxSize / maxPrime constraints (the C# code simply omits it).
export function lcmFamily(
  targetLcm: number,
  maxSize = 24,
  maxPrime = 5,
): Fraction[] {
  if (targetLcm < 1) return [];

  const fractions = goodFractions(maxSize, maxPrime);
  const members: Fraction[] = [];
  let foldedLcm = 1;
  for (const f of fractions) {
    if (targetLcm % f.denominator !== 0) continue;
    members.push(f);
    foldedLcm = lcm(foldedLcm, f.denominator);
  }

  return foldedLcm === targetLcm ? members : [];
}

// --- Sine.cs ---------------------------------------------------------------

// Sample sin(2π · frequencyRatio · t) over `durationPeriods` reference periods
// at `samplesPerPeriod` points per period. count = samplesPerPeriod*duration + 1
// so the endpoint t = durationPeriods is included.
export function sampleSine(
  frequencyRatio: number,
  durationPeriods: number,
  samplesPerPeriod: number,
): {t: Float64Array; y: Float64Array} {
  const count = samplesPerPeriod * durationPeriods + 1;
  const t = new Float64Array(count);
  const y = new Float64Array(count);
  const dt = 1.0 / samplesPerPeriod;
  const omega = 2 * Math.PI * frequencyRatio;
  for (let i = 0; i < count; i++) {
    t[i] = i * dt;
    y[i] = Math.sin(omega * t[i]);
  }
  return {t, y};
}
