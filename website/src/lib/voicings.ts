// Faithful TypeScript port of the C# voicing scorer, so the chord player can voice a
// held chord with the lowest-penalty ascending, semitone-avoiding ordering instead of
// stacking every note inside one octave. Mirrors src/Music/Voicings.cs. The C# code
// stays the source of truth; verify parity against
// `melodroid table voicings --keys ... --ktet 12`.

// Penalty per ascending interval (Voicings.cs Penalty): thirds/fourths free, seconds and
// fifths mild, wides increasingly costly, semitones/unisons forbidden.
export function penalty(interval: number): number {
  if (interval === 3 || interval === 4) return 0;
  if (interval === 2 || interval === 5) return 1;
  if (interval >= 6) return interval - 4;
  return Number.MAX_SAFE_INTEGER / 2;
}

export type Voicing = {
  root: number; // the bottom pitch class, drawn from the input keys
  offsets: number[]; // ascending absolute semitone offsets from the root: [0, i1, i1+i2, …]
  span: number; // total ascent (sum of intervals)
  penalty: number; // summed interval penalty
};

// Mirror of Voicings.Search: from `currentPitch`, extend the ascending voicing with every
// not-yet-used placement key, skipping semitone/unison steps, accumulating penalty.
function search(
  currentPitch: number,
  offsets: number[],
  used: Set<number>,
  placement: number[],
  ktet: number,
  runningPenalty: number,
  root: number,
  out: Voicing[],
): void {
  if (used.size === placement.length) {
    const span = offsets[offsets.length - 1];
    out.push({root, offsets: [...offsets], span, penalty: runningPenalty});
    return;
  }

  const currentResidue = ((currentPitch % ktet) + ktet) % ktet;
  for (const p of placement) {
    if (used.has(p)) continue;
    const delta = ((((p - currentResidue) % ktet) + ktet) % ktet);
    if (delta === 0 || delta === 1) continue;

    const next = currentPitch + delta;
    offsets.push(offsets[offsets.length - 1] + delta);
    used.add(p);

    search(next, offsets, used, placement, ktet, runningPenalty + penalty(delta), root, out);

    offsets.pop();
    used.delete(p);
  }
}

// Enumerate every ascending, semitone-avoiding voicing from every distinct root
// (Voicings.EnumerateAll).
export function enumerateAll(keys: number[], ktet = 12): Voicing[] {
  const distinct = [...new Set(keys)].sort((a, b) => a - b);
  const out: Voicing[] = [];
  for (const root of distinct) {
    search(root, [0], new Set([root]), distinct, ktet, 0, root, out);
  }
  return out;
}

// The globally lowest-penalty voicing, tie-broken by smallest span (matching the ordering
// EnumerateBestPerRoot applies within each root, extended across roots). Returns null when
// no semitone-free voicing exists (e.g. a bare semitone dyad {0, 1}).
export function bestVoicing(keys: number[], ktet = 12): Voicing | null {
  let best: Voicing | null = null;
  for (const v of enumerateAll(keys, ktet)) {
    if (best === null || v.penalty < best.penalty || (v.penalty === best.penalty && v.span < best.span)) {
      best = v;
    }
  }
  return best;
}
