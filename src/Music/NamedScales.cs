namespace Melodroid_3.Music;

// A named scale as a root-oriented necklace: the pitch classes of the scale rooted on key 0, sorted
// ascending. "Rooted on 0" (not music-set-theory normal order) so that transposing by an anchor a
// yields the scale rooted on key a — i.e. a ScaleRow at anchor a reads as "{Name} @a", matching the
// progression-principles doc convention (harm@9 = harmonic minor rooted on 9 = {0 2 4 5 8 9 11}).
public readonly record struct NamedScale(string Name, IReadOnlyList<int> Necklace);

// One qualifying scale placed at a concrete anchor (= its root). Keys are the transposed necklace,
// sorted ascending in [0, ktet).
public readonly record struct ScaleRow(string Name, int Anchor, IReadOnlyList<int> Keys);

// The fixed catalog of named scales used by `table scale-fingerprint`, and the row-builder that
// transposes them to every anchor containing a chord.
//
// The names are empirical labels on results derived from the subset principle, NOT the derivation
// (per the "first principles, not tradition" anchor). Provenance of each necklace:
//   - major         {0 2 4 5 7 9 11}    = the lcm-24 placement 24@0.
//   - harmonic minor{0 2 3 5 7 8 11}    = junction 15@11·18@2·24@0 (stable), as harmonic minor rooted on 0.
//   - melodic minor {0 2 3 5 7 9 11}    = junction 15@11·18@2·24@7 (stable), as melodic minor rooted on 0.
//   - bebop dominant{0 2 4 5 7 9 10 11} = junction 24@0·18@7·24@5 (= bebop@0).
//   - harmonic major{0 2 4 5 7 8 11}    = junction 15@11·5@4·15@4 (stable), as harmonic major rooted on 0.
//   - major blues   {0 2 3 4 7 9}       = the documented sub-selection of the bebop dominant union
//                                         (24@0 ∪ 24@5 minus the lcm-24 roots), as major blues rooted on 0.
// NamedScalesTests asserts the four junction necklaces are transposition classes of JunctionScales
// outputs, major equals the 24@0 placement, and major blues is a sub-selection of a bebop necklace.
public static class NamedScales
{
    public static IReadOnlyList<NamedScale> Catalog { get; } = new[]
    {
        new NamedScale("major",          new[] { 0, 2, 4, 5, 7, 9, 11 }),
        new NamedScale("harmonic minor", new[] { 0, 2, 3, 5, 7, 8, 11 }),
        new NamedScale("melodic minor",  new[] { 0, 2, 3, 5, 7, 9, 11 }),
        new NamedScale("bebop dominant", new[] { 0, 2, 4, 5, 7, 9, 10, 11 }),
        new NamedScale("harmonic major", new[] { 0, 2, 4, 5, 7, 8, 11 }),
        new NamedScale("major blues",    new[] { 0, 2, 3, 4, 7, 9 }),
    };

    // One row per (scale, anchor) whose transposed key set fully contains chordKeys. Each catalog
    // necklace is shifted to all `ktet` anchors (necklace + anchor mod ktet); an anchor qualifies
    // iff its key set ⊇ the chord. Rows keep catalog order, then ascending anchor, for stable display.
    public static IReadOnlyList<ScaleRow> ContainingChord(
        IReadOnlyCollection<int> chordKeys, int ktet = 12)
    {
        var chord = new HashSet<int>(chordKeys.Select(k => ((k % ktet) + ktet) % ktet));
        var rows = new List<ScaleRow>();

        foreach (var scale in Catalog)
        {
            for (var anchor = 0; anchor < ktet; anchor++)
            {
                var keys = scale.Necklace
                    .Select(k => (k + anchor) % ktet)
                    .OrderBy(k => k)
                    .ToList();
                if (chord.IsSubsetOf(keys))
                    rows.Add(new ScaleRow(scale.Name, anchor, keys));
            }
        }

        return rows;
    }
}
