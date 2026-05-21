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

Run `dotnet run -- graph lcm-families` to generate a Mermaid graph at `output/graphs/lcm-families.md` illustrating the three relations (solid arrow = literal subset, thick double-headed arrow = isomorphism, dashed arrow = renormalized subset). Open the file in VSCode and press Ctrl+Shift+V to view the rendered graph. Edges are Hasse-reduced per relation and isomorphic families are grouped into subgraph clusters. Options: `--max-size` (default 24), `--max-prime` (default 5), `--max-lcm` (default 24).