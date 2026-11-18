namespace Melodroid_3.Music;

// A named scale as a root-oriented necklace: the pitch classes of the scale rooted on key 0, sorted
// ascending. "Rooted on 0" (not music-set-theory normal order) so that transposing by an anchor a
// yields the scale rooted on key a — i.e. a match at anchor a reads as "{Name} @a", matching the
// progression-principles doc convention (harm@9 = harmonic minor rooted on 9 = {0 2 4 5 8 9 11}).
public readonly record struct NamedScale(string Name, IReadOnlyList<int> Necklace);

// The catalog of traditional scale names used *cosmetically* across the CLI (table chord-melody,
// analyze midi, table progression). The scales themselves are derived from first principles (the subset
// principle / CompositeScales) — this catalog is only a lookup from a concrete key set to its empirical
// traditional name, never a source of scales. `Match` maps a key set to its name; everything derivational
// lives elsewhere.
//
// The names are empirical labels on results derived from the subset principle, NOT the derivation
// (per the "first principles, not tradition" anchor). Provenance of each necklace:
//   - major         {0 2 4 5 7 9 11}    = the lcm-24 placement 24@0.
//   - harmonic minor{0 2 3 5 7 8 11}    = composite 15@11·18@2·24@0 (stable), as harmonic minor rooted on 0.
//   - melodic minor {0 2 3 5 7 9 11}    = composite 15@11·18@2·24@7 (stable), as melodic minor rooted on 0.
//   - bebop dominant{0 2 4 5 7 9 10 11} = composite 24@0·18@7·24@5 (= bebop@0).
//   - harmonic major{0 2 4 5 7 8 11}    = composite 15@11·5@4·15@4 (stable), as harmonic major rooted on 0.
//   - major blues   {0 2 3 4 7 9}       = the documented sub-selection of the bebop dominant union
//                                         (24@0 ∪ 24@5 minus the lcm-24 roots), as major blues rooted on 0.
// NamedScalesTests asserts the four composite necklaces are transposition classes of CompositeScales
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

    // Compact display codes for the catalog scales, case-sensitive (hm ≠ hM). Keyed by catalog Name.
    // Used to keep the cosmetic Scales / Composite labels readable; the full names stay the source of
    // truth (table chord-melody) — this is a display concern only.
    private static readonly IReadOnlyDictionary<string, string> Shorthands = new Dictionary<string, string>
    {
        ["major"] = "M", ["harmonic minor"] = "hm", ["melodic minor"] = "mm",
        ["bebop dominant"] = "bb", ["harmonic major"] = "hM", ["major blues"] = "b",
    };

    public static string Shorthand(string name) => Shorthands[name];

    // "M=major, hm=harmonic minor, …" built from the catalog so the legend can never drift out of
    // sync with the shorthand map or the scale set.
    public static string Legend() =>
        string.Join(", ", Catalog.Select(s => $"{Shorthand(s.Name)}={s.Name}"));

    // The (Name, Anchor) of the catalog scale whose transposed necklace EXACTLY equals keys, or
    // null when no catalog scale matches. Anchor is the root, per the "{Name} @anchor" convention.
    // The key set must coincide with the whole scale. Used to name a composite scale (the stable union
    // of a subset bridge's two placements) when it lands on a known scale; callers fall back to the raw
    // constituents otherwise. This is the only cosmetic-naming entry point.
    public static (string Name, int Anchor)? Match(IReadOnlyCollection<int> keys, int ktet = 12)
    {
        var set = new HashSet<int>(keys.Select(k => ((k % ktet) + ktet) % ktet));

        foreach (var scale in Catalog)
        {
            if (scale.Necklace.Count != set.Count) continue;
            for (var anchor = 0; anchor < ktet; anchor++)
            {
                if (scale.Necklace.All(k => set.Contains((k + anchor) % ktet)))
                    return (scale.Name, anchor);
            }
        }

        return null;
    }
}
