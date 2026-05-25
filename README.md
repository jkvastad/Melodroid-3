# Melodroid 3
A program for exploring music.

## Theoretical premise
Investigating the boundary conditions around which musical perception evolved.
Analogous to studying cuisine - knowledge of e.g. taste buds can inspire great recipes.

## Harmony as pattern recognition of simultaneous waves
We will be studying musical harmony via pattern recognition of simultaneous waves. For example a major chord can be expressed as fractions {1, 5/4, 3/2}. 
The pattern for this set of frequencies loops every 4 iterations of the base wavelength, extending over 4 periods of time. 
The wave pattern length (WPL) is the Least Common Multiple (LCM) of the sets denonimators.

### Good Fractions
When expressing simultaneous musical notes as fractions of a reference note, some fractions will be better than others for creating repeating patterns in simultaneous waves.

Given the lower frequency hearing threshold for humans of 20hz, a wave pattern extending over more than 50ms will likely not be recognized/pattern matched.
* E.g. for a fundamental of 100hz the major chord with WPL 4 the wave pattern duration (WPD) is 40ms.

Music (at least orchestra/opera) is mostly played in C2-C6 range (65hz - 1047hz fundamental range, let's say 100hz - 1000hz for simplicity). 
* Lower frequencies produce longer duration patterns, thus 100hz can be used as a worst case for WPD.

Denominators of fractions in a set determine the WPL and music is octave equivalent - all fractions are mapped onto "[1,2)": 
* Any note in a set may be renormalized as the base wavelength - all resulting fractions less than one are then multiplied by 2 until larger than one 
** e.g. {1, 5/4, 3/2} -> renormalize with 5/4 as base wavelength 1 {4/5, 1, 12/10} -> octave normalize {8/5, 1, 12/10} -> simplify and sort {1, 6/5, 8/5}.
* This means numerators effectively have the same constraints as denominators.

Harmony requires multiple different frequencies, which by the above implies there are only so many "good fractions" which can be used to construct wave patterns.
* Worst case of 100hz = 10ms means maximum WPL of 5. This is quite low and does not permit many different patterns. Constraint limits are fuzzy and the real WPL is likely higher.
* Since WPL is decided by LCM, having denominators constructed from small primes (e.g. 2, 3 and 5) is more lenient towards total WPL as they may cancel out with other denominators on renormalization to different base wavelengths.

In view of this, perhaps WPL of up to 24 should be allowed - highly influenced by the fact that the major scale played as a chord (e.g. G13/Cmaj13 for C major) can be expressed as a pattern of WPL 24
* Common twelve-tone equal temperament (12-TET) has notes approximated notably by largest denominator 15 for semitone (16/15)

Possible good fractions are thus a function of maximum allowed denominator/numerator size (e.g. 24) and highest allowed prime (5).

Run `dotnet run -- table good-fractions` to print the good fractions. Options: `--max-size` (default 24), `--max-prime` (default 5).

### LCM Families
Given a set of good fractions, we can compute the LCM for the denominators of a given non-empty subset (this LCM is the WPL for that fraction set). For a given LCM L, the maximum sized subset of the good fractions whose denominators have LCM L is called the LCM Family (for those good fractions) of size L.

Run `dotnet run -- table lcm-families` to print the LCM families, ordered by ascending LCM, with empty families omitted. Options: `--max-size` (default 24), `--max-prime` (default 5), `--max-lcm` (default 24).

#### Isomorphisms and Subsets
Some LCM families are naturally subsets of others, e.g. LCM4 {1, 5/4, 3/2} is a subset to LCM8{1/1, 9/8, 5/4, 3/2, 15/8}. Due to octave equivalence, some families are subsets after renormalizing (lcm 18: {1/1, 10/9, 4/3, 3/2, 5/3, 16/9} renormalize to 4/3 -> {1/1, 9/8, 5/4, 4/3, 3/2, 5/3}, subset of 24), or even completely isomorphic (LCM4 {1, 5/4, 3/2} renormalize to 3/2 -> LCM3{1, 4/3, 5/3}).

Run `dotnet run -- graph lcm-families` to generate a Mermaid graph at `output/graphs/lcm-families.md` illustrating the three relations (solid arrow = literal subset, thick double-headed arrow = isomorphism, dashed arrow = renormalized subset). Open the file in VSCode and press Ctrl+Shift+V to view the rendered graph. Edges are Hasse-reduced per relation and isomorphic families are grouped into subgraph clusters. Options: `--max-size` (default 24), `--max-prime` (default 5), `--max-lcm` (default 24), `--mode` (default `full`).

Add `--mode collapsed` to instead generate `output/graphs/lcm-families-collapsed.md`, where each isomorphism class (singletons included) becomes a single node. Class-to-class edges are deduplicated and Hasse-reduced per kind, so e.g. you see `class 1 -> class 2` once rather than every `LCM=3 -> LCM=5`, `LCM=3 -> LCM=6`, `LCM=4 -> LCM=5`, `LCM=4 -> LCM=6` arrow that the full view emits. Intra-class isomorphism edges disappear by construction. Useful when the full graph has too many edges to scan at a glance.

#### Wave Pattern Plots
The LCM of a family is its Wave Pattern Length: the number of reference periods over which the in-phase superposition of its fractions repeats. We can see this by plotting each fraction `p/q` as `sin(2π · (p/q) · t)` (amplitude 1, all in phase at `t = 0`) on a shared time axis from `0` to `LCM`, together with their sum. Every component sine completes an integer number of cycles by `t = LCM` and returns to zero, so the summed pattern closes at the endpoint — the wave pattern visibly repeats with period LCM.

Run `dotnet run -- plot lcm-families --lcm 6` to write `output/plots/lcm-family-6.png`. The PNG overlays each fraction's sine in its own color and draws the superposition in thick black. A dashed vertical line marks `t = LCM`. Options: `--lcm` (required), `--max-size` (default 24), `--max-prime` (default 5), `--samples-per-period` (default 200; controls plot smoothness), `--mode` (default `all`).

Add `--mode sum` to plot only the superposition (writes `output/plots/lcm-family-{lcm}-sum.png`), or `--mode constituents` to plot only the individual sine waves with no sum overlay (writes `output/plots/lcm-family-{lcm}-constituents.png`). The Y-axis range is identical across all three modes so the views are directly comparable.

Add `--subset-lcm K` to highlight a literal-subset family inside the parent plot. Because a family's WPL equals its LCM, whenever `K` divides `--lcm` the sub-family's wave pattern tiles `--lcm / K` times across the parent's window — e.g. lcm4 fits 2× into lcm8, lcm3 fits 8× into lcm24. The plot then adds blue dashed vertical lines at each multiple of `K`, plus the superposition of just the sub-family's sines drawn in solid blue, making the repeating sub-pattern visible against the parent's full waveform. `K` must divide `--lcm` (the literal-subset condition) and the family at `K` must exist under the current `--max-size` / `--max-prime`. The filename gets a `-sub{K}` suffix, e.g. `output/plots/lcm-family-24-sub3.png`.

Combine with `--mode difference` to isolate the residual — the parent's superposition minus the subset's superposition. By default the panel overlays three curves on a shared axis: the parent sum (black), the subset sum (blue), and the difference (red), with the subset's dashed period markers retained for spatial reference. This makes visible what the parent family contributes *beyond* what the subset already accounts for: where the subset captures the bulk of the pattern, the residual is small and tracks under the parent and subset sums; where the parent adds genuinely new structure, the residual swings wide. Add `--difference-only` to drop the two reference sums and plot just the red residual when you want a clean look at it. Requires `--subset-lcm`; filename is `output/plots/lcm-family-{lcm}-difference-sub{K}.png` (or `-difference-only-sub{K}.png` with the flag).

### Cluster ranges
Continuing on the idea of LCM families with isomorphism under octave equivalence, we have discussed sound as sets of fractions. Real sound, however, does not consist of perfect fractions. If harmony is pattern recognition of frequency fractions, then it is likely that some sort of clustering is performed by the listener, where the ratios of musical notes are binned or mapped onto their ideal fractions.

Given a set of fractions on the interval [1,2), using the Just Noticeable Difference for humans (about 0.5-1% depending on Hz) produces a set of relative bins around an ideal fraction towards which ratios can cluster (a cluster target).

A problem arises if two such clusters were to overlap - namely, which cluster target does a ratio cluster to if it falls in the overlap? If bin radius is expressed as a ratio c then the overlap point between two fractions a < b has the relation:

a + ac = b - bc -> c = (b-a)/(b+a)

Run `dotnet run -- table bin-overlaps` to print, for each adjacent pair of good fractions, the bin radius `c` at which their JND clusters first touch. Each row shows `(Lower, Upper, c, c %)` where `c = (Upper - Lower) / (Upper + Lower)` per the relation above. The `c` column is the *exact* reduced fraction (e.g. `1/161` for the tightest gap `10/9 → 9/8`); `c %` is the same value as a percentage (e.g. `0.621 %`) to align with the README's "about 0.5–1% JND" framing. The final row handles the octave wrap by pairing the largest good fraction with a virtual `2/1` (which is octave-equivalent to `1/1`), so a set of N good fractions produces N rows. The table caption surfaces the minimum `c` — the binding constraint, i.e. the largest JND a listener could have before *any* adjacent good-fraction bins start to overlap. Options: `--max-size` (default 24), `--max-prime` (default 5).

#### Full Match and Octave Sweep
While any frequency can be used as a reference point to express a set of frequencies as ratios, some reference points will create a set of good fractions so that their combined LCM is relatively low. Given a set of ratios on [1,2) it is possible to sample reference points along this interval, renormalising the given ratios and binning them (cyclically - a value close to 2 can bin to 1) to the good fractions with some given bin radius. I will call this procedure an Octave Sweep.

For bin radius less than 1/161 there are no overlapping bins and the results are unambiguous. If all of the ratios in a set bin to good fractions for a given bin radius and reference point in an octave sweep, I will call it a Full Match (of the octave sweep).

Run `dotnet run -- table octave-sweep --ratios 1.0 1.25 1.5` to sweep a reference ratio across `[1, 2)` and bin each input ratio against the good fractions. 
Each row shows:
* The reference ratio
* One cell per good-fraction column (signed percentage distance relative the good fraction if the renormalized input ratio fell inside that fraction's bin, otherwise empty)
* The LCM of the denominators of the good fractions that received a binning
* A `Full?` marker. 
Rows where every input ratio bins to exactly one good fraction (Full Match) are highlighted green. If any renormalized ratio falls inside two overlapping bins (only possible at bin radius ≥ 1/161), the entire row is flagged ambiguous: each competing good-fraction column is filled in for that input (one rendered cell per match), all with a `~` prefix so the conflict is visible at a glance; the `LCM` cell is blank, and the row is highlighted yellow. Ambiguous-overlap rows are still treated as full matches by the filter — every input *did* land in a bin, only the LCM is undefined — so `--only-full-matches` keeps them too.
Options: `--ratios` (required, space-separated decimals on `[1, 2)`), `--sweep-step` (default `0.001`, unitless ratio increment — 1000 rows across the octave), `--bin-radius` (default `1/161 ≈ 0.00621`, the unambiguous threshold), `--only-full-matches` (default off; suppress rows where any input fell outside every bin — strict full matches and ambiguous-overlap rows are both kept; the caption still reports the full sweep count and the ambiguous tally), `--max-size` (default 24), `--max-prime` (default 5).

Some common ratios for reference:
(lcm 4) 1.0 1.25 1.5
(lcm 8) 1.0, 1.125, 1.25, 1.5, 1.875

##### Centered Full Matches
Finding full matches in an octave sweep is similar to tuning a radio - at certain reference ratios we get full matches. These generally appear in contiguous blocks of sweep steps — adjacent steps which renormalize the given rations into the bins of the same set of good fractions. These sweep steps are all technically full matches even though they describe the same set of good fractions.

A **centered full match** is the single best step in such a block: the one with the smallest worst-cell distance (`min` over the block of `max` over cells of `|signed % distance|`). Ties are broken by the lower sweep index. The block itself is the maximal contiguous run of full-match rows that share the same per-input-position tuple of matched good fractions — so when an octave sweep transitions directly from one tuning (e.g. `{1, 5/4, 3/2}`) to a competing one (e.g. `{1, 6/5, 3/2}`) without a non-full-match row between them, each tuning still gets its own centered row.

Centered rows are marked `★` in the `Full?` column (instead of `✓`) and the table caption reports the centered count alongside the full-match count. Use `--only-centered-full-matches` to suppress every other row, leaving exactly one row per block. Ambiguous-overlap rows participate in blocks alongside strict full matches (they share the first-matched-fraction signature), so the centered row of a block may itself be ambiguous — that row keeps its yellow highlight even when marked `★`. The centered filter takes precedence over `--only-full-matches` if both are passed.

### Playing Fractions
To play music, an instrument is needed. One one end we have instruments which can produce arbitray frequencies, such as violins and the human voice, allowing full expression of sound. Such freedom of expression also comes with a high complexity as the choices are only limited by the resolution of frequencies.

The current hypothesis builds on the idea of clustering imperfect ratios to good fractions, meaning that perfect expression is not necessarily needed for perfect harmony, nor perhaps even desirable. Supposing there are only specific frequency relations of importance means adding meaningless frequencies to an instrument is unnecessary. This is a point in favor of studying instruments with keys, producing a subset of all possible frequencies, removing unnecessary complexity. 

As a counterpoint, sometimes a great work needs contrast. Dissonance itself might be desirable: this idea is similar to how chili, a toxin, is indisputably popular in cuisine. A naive culinary model could reasonably label all toxins as undesireable, and we might do likewise were we to label all non-harmonious ratios undesirable.

Which frequency relations are important, and ultimately, which ratios are necessary for an instrument?

#### A Good Keyboard
A desired property when playing a keyed instrument is modulation. Modulation refers to playing the same ratios of frequencies from different fundamentals while still producing the same harmonies. More formally we can say that akeyboard can express all possible full matches from every key.

For an arbitrary keyboard having access to the good fractions from one reference point does not guarantee having them from others. This last point can be solved by equal temperament tonal systems, where the ratio of any two adjacent keys is the same, making all fundamentals equal.

Combining equal temperament tuning with the need for octave equivalency, we are now looking at instruments producing frequencies of the form (f_0*2^(n/k)), where f_0 is some arbitrary frequency (e.g. the Standard Pitch at 440Hz), n is the distance in number of keys from the middle key, and k is the number of keys in the tuning. Setting k to 12 in this k-tone equal temperament (k-tet) system we get the well known 12-tet tuning.

##### How many keys are needed?
To answer the question of how many keys are needed, let’s begin by listing the requirements set by the theory to see indeed what the keys are needed for:

* Modulation: 
It must be possible to play all the good fractions using any key as reference point. This means that relative to any arbitrary key in the equal tuning, for each good fraction there must be at least one key within the fraction's bin radius.
On one hand having infinite keys technically solves the problem, but maximizes complexity.
On the other hand having a single key with bin radius set to half an octave also technically works, but is way outside any JND interpretation.

* Full Expression - No unplayable virtual reference points
A set of keys might produce a full match with a virtual reference point, one which is not in the set of played keys. For example a Major Chord (1, 5/4, 3/2) has a full match of LCM 8 at 4/3.
Such a virtual reference point should be playable from a key on the keyboard.

* Minimal Complexity - as few keys as possible

The above requirements would allow us to play all the good fractions from any key and physically sound any reference point creating a full match with minimal key clutter.

Here we encounter a subtle yet important point: what does it mean to "play a good fraction using a key"? Previously we looked at arbitrary ratio sets on [1,2) and sampled reference points to search for full matches. With a keyboard our ratio sets are defined by the fixed ratios between keys (which is just a smaller set of possible ratios). However, we are no longer free to arbitrarily sound a reference point - it must also correspond to a key.

For a given keyboard with a given a set of keys which have a full match, the reference point has a closest key. Intuitively, if the key is close enough we will recognize it as the reference point. How close is close enough? Must the reference be within JND of the key? Is it a larger bin radius due to inference from context (the given set of keys helping out)? Since full matches are based on the concept of binning real ratios to ideal fractions, using the bin radius to bin the closest key to the reference point seems appropriate.

With such a definition of "playing a good fraction using a key", namely the sounded key frequency expressed as a ratio must bin to the target ideal good fraction, let's construct our keyboard.

###### Modulation
To satisfy the requirement of modulation we can look at a k-tet system. Given a reference key each good fraction must have at least one key in the k-tet within bin radius. There are two parameters affecting this: bin radius and number of keys. For a given number of keys, there will be a good fraction which needs the largest radius to bin its nearest key. This bin radius is `c_k = |2^(n/k) − g_k*| / g_k*` — the worst-case multiplicative distance "caused" by `g_k*`.

Run `dotnet run -- table ktet-cutoffs` to list, for each `k` from 1 to `--max-k`, the cutoff `c_k`, the limiting fraction `g_k`, and the nearest k-tet key (index `n` and ratio `2^(n/k)`). Green rows mark where `c_k` strictly improves over every smaller k. Add `--only-strictly-improving` to keep only those green rows; the caption still reports the full active k sequence and adds a `(filter: only strictly improving)` clause. Options: `--max-size` (default 24), `--max-prime` (default 5), `--max-k` (default 50), `--only-strictly-improving` (default off).

Notably for standard good fractions (max size 24, max prime 5), we get
┌────┬──────────┬───────────┬───────────────────┬───────────┬──────────┐
│  k │      c_k │     c_k % │ Limiting Fraction │ Nearest n │  2^(n/k) │
├────┼──────────┼───────────┼───────────────────┼───────────┼──────────┤
│ 11 │ 0.029332 │  2.9332 % │ 5/4               │         4 │ 1.286665 │
│ 12 │ 0.010216 │  1.0216 % │ 10/9              │         2 │ 1.122462 │
│ 19 │ 0.008460 │  0.8460 % │ 16/15             │         2 │ 1.075691 │
│ 31 │ 0.006458 │  0.6458 % │ 10/9              │         5 │ 1.118287 │
│ 34 │ 0.004547 │  0.4547 % │ 9/8               │         6 │ 1.130116 │

Since JND is almost 1%, at k=11 the bin radius is about 3x JND but suddenly drops to JND at k=12. This only improves (slightly, some 20% smaller) at k=19, and then again at 31, 34 - just around the threshold for unique good fraction bin radii. Thus k=12 with about 63% of the keys for k=19 seems like a good compromise. This is quite remarkable as we have just recovered a (in some sense the) modern popular tuning system from first principles of biology and physics.

###### Full Expression
To satisfy the requirement of full expression any set of keys on our keyboard which has a full match must be able to play a key which bins to the reference point of that full match. Since our solution for modulation makes sure that every key can function as a reference point to every good fraction, this requirement is also fulfilled by k-tets with sufficiently low bin radii.

###### Minimal Complexity
To satisfy the requirement of minimal complexity, we need as few keys as possible when fulfilling the other requirements. Since modulation was solved with this in mind, our ktet choices seem to be between 12, 19, 31 and 34 keys. This would favor 12-tet as the optimal solution, with two caveats:
* The bin radius might be to large and require 19 or even 34 (unique binnings) keys.
* The solution might be too perfect - basically we might allow too little dissonance (see discussion of cuisine and chili in section "Playing Fractions"). In music we have effects such as bending strings on guitar which slides between notes. Perhaps the effect is popular due to introducing the right amount of dissonance, or perhaps it is simply the novelty of a sliding frequency - perhaps both.