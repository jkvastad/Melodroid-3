namespace Melodroid_3.Music;

// A named scale as a root-oriented necklace: the pitch classes of the scale rooted on key 0, sorted
// ascending. "Rooted on 0" (not music-set-theory normal order) so that transposing by an anchor a
// yields the scale rooted on key a — i.e. a ScaleRow at anchor a reads as "{Name} @a", matching the
// progression-principles doc convention (harm@9 = harmonic minor rooted on 9 = {0 2 4 5 8 9 11}).
public readonly record struct NamedScale(string Name, IReadOnlyList<int> Necklace);

// One qualifying scale placed at a concrete anchor (= its root). Keys are the transposed necklace,
// sorted ascending in [0, ktet).
public readonly record struct ScaleRow(string Name, int Anchor, IReadOnlyList<int> Keys);

// The subset-principle decomposition of a catalog scale into its constituent LCM-family placements,
// transposed to a concrete anchor. For a junction scale PA/S/PB are the P_A · S · P_B of the subset
// bridge whose stable union is the scale (NamedScales.Decompose). `major` has a single placement
// (24@anchor) with S/PB null; scales with no clean junction (`major blues`) are not decomposable.
public readonly record struct ScaleConstituents(string Name, Placement? PA, Placement? S, Placement? PB);

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

    // Compact display codes for the catalog scales, case-sensitive (hm ≠ hM). Keyed by catalog Name.
    // Used to keep the `analyze midi` Scales / Junction columns readable; the full names stay the
    // source of truth (ScaleRow.Name, table scale-fingerprint) — this is a display concern only.
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

    // The (Name, Anchor) of the catalog scale whose transposed necklace EXACTLY equals keys, or
    // null when no catalog scale matches. Anchor is the root, per the "{Name} @anchor" convention.
    // Distinct from ContainingChord, which tests subset containment; here the key set must coincide
    // with the whole scale. Used to name a junction scale (the stable union of a subset bridge's two
    // placements) when it lands on a known scale; the caller falls back to the raw key set otherwise.
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

    // Subset-principle provenance of each catalog scale as (Lcm, At) per constituent slot (P_A, S, P_B)
    // — the junction decompositions documented in the catalog comments above, at *some* base rooting
    // (the stable union need not sit on root 0: 15@11·18@2·24@0 is harmonic minor rooted on 9).
    // Decompose finds the actual root via Match and re-anchors, so these base values only have to
    // define the right scale, not a specific root. `major` is a lone 24@0 placement (S/P_B absent);
    // `major blues` has no clean junction and is omitted, so Decompose returns null for it.
    // NamedScalesTests asserts each decomposition's stable union (JunctionScales.UnionKeys) equals the
    // catalog necklace at the requested anchor, pinning this table to first principles.
    private static readonly IReadOnlyDictionary<string, (int Lcm, int At)?[]> Provenance =
        new Dictionary<string, (int Lcm, int At)?[]>
        {
            ["major"]          = new (int Lcm, int At)?[] { (24, 0), null, null },
            ["harmonic minor"] = new (int Lcm, int At)?[] { (15, 11), (18, 2), (24, 0) },
            ["melodic minor"]  = new (int Lcm, int At)?[] { (15, 11), (18, 2), (24, 7) },
            ["bebop dominant"] = new (int Lcm, int At)?[] { (24, 0), (18, 7), (24, 5) },
            ["harmonic major"] = new (int Lcm, int At)?[] { (15, 11), (5, 4), (15, 4) },
        };

    // The constituent placements of the named scale rooted on `anchor`, or null when the scale has no
    // decomposition (unknown name, or `major blues`). The provenance triple defines the scale at some
    // base root; we recover that root from its stable union via Match, then re-anchor every slot by
    // (anchor - root) so the decomposition is rooted on `anchor`. Placements are built through the one
    // key-construction path (Placements.Compute); a missing LCM family yields a null slot. 12-tet only.
    public static ScaleConstituents? Decompose(
        string name, int anchor, IReadOnlyList<LcmFamily> families, int ktet)
    {
        if (!Provenance.TryGetValue(name, out var slots)) return null;

        Placement? Build((int Lcm, int At)? slot, int shift)
        {
            if (slot is null) return null;
            var (lcm, at) = slot.Value;
            var family = families.FirstOrDefault(f => f.Lcm == lcm);
            if (family.Fractions is null || family.Fractions.Count == 0) return null;
            return Placements.Compute(family, (((at + shift) % ktet) + ktet) % ktet, ktet);
        }

        var pa0 = Build(slots[0], 0);
        if (pa0 is null) return null;
        var pb0 = Build(slots[2], 0);
        var union0 = pb0 is { } b0
            ? JunctionScales.UnionKeys(pa0.Value, b0, stable: true, ktet)
            : pa0.Value.Keys;
        if (Match(union0, ktet) is not { } m) return null;
        var shift = (((anchor - m.Anchor) % ktet) + ktet) % ktet;

        return new ScaleConstituents(name, Build(slots[0], shift), Build(slots[1], shift), Build(slots[2], shift));
    }
}
