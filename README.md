# Melodroid 3
A program for exploring music.

## Theoretical premise
Investigating the boundary conditions around which musical perception evolved.
Analogous to studying cuisine - knowledge of e.g. taste buds can inspire great recipes.


## Harmony as pattern recognition of simultaneous waves
We will be studying musical harmony via pattern recognition of simultaneous waves. For example a major chord can be expressed as fractions {1, 5/4, 3/2}. 
The pattern for this set of frequencies loop every 4 iterations of the base wavelength, extending over 4 periods of time. 
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

Run `dotnet run -- table octave-sweep --ratios 1.0 1.25 1.5` to sweep a reference ratio across `[1, 2)` and bin each input ratio against the good fractions. Each row shows the reference, one cell per good-fraction column (signed percentage distance relative the good fraction, if the renormalized input ratio fell inside that fraction's bin, otherwise empty), the LCM of the denominators of the good fractions that received a binning, and a `Full?` marker. Rows where every input ratio bins to exactly one good fraction (Full Match) are highlighted green. If any renormalized ratio falls inside two overlapping bins (only possible at bin radius ≥ 1/161), the entire row is flagged ambiguous: cells from the ambiguous input get a `~` prefix, the `LCM` cell is blank, and the row is highlighted yellow. Options: `--ratios` (required, space-separated decimals on `[1, 2)`), `--sweep-step` (default `0.001`, unitless ratio increment — 1000 rows across the octave), `--bin-radius` (default `1/161 ≈ 0.00621`, the unambiguous threshold), `--only-full-matches` (default off; suppress rows that aren't a Full Match — the table caption still reports the full sweep count), `--max-size` (default 24), `--max-prime` (default 5).