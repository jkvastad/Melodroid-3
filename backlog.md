# Docs
* Note that there is a high severity vulnerability in serialize-javascript <=7.0.4 which is a false alarm because the only input which could be rce is on build side. Just wait for docusaurus to patch it out. Do not npm audit fix --force, which would attempt a breaking major upgrade.

Needs general fixing, several bugs and uglies. Sections below mirror the website doc structure (website/docs/).

Mixed usage of katex for e.g. hz - sometimes inline mathrm, sometimes just plain text.

## Intro (intro.mdx)

## Theory
Perhaps the docusaurus default boxed layout of subsections should instead be a list as sections build on each other top to bottom?
### Good Fractions (theory/good-fractions.mdx)
### LCM Families (theory/lcm-families.mdx)
TODO: add in mermaid graph for "graph lcm-families --mode full --max-size 16 --max-lcm 12" - perhaps 16 is the largest lcm and the isomorphic 8-12 group is the largest lcm family, with 15, 18, 20 and 24 just being melody style superpositions?
### Cluster Ranges (theory/cluster-ranges.mdx)
### Playing Fractions (theory/playing-fractions.mdx)
## The Keyboard
### A Good Keyboard (keyboard/a-good-keyboard.mdx)
### Key Sweep (keyboard/key-sweep.mdx)

## The Sound of Music

### Voicings and Placements (music/voicings-and-placements.mdx)
### Voicings and LCM Families (music/voicings-and-lcm-families.mdx)
TODO: chord player draws melody from one match at a time via drop down list, perhaps expand it to some sort of toggle list so the user can create superpositions of the available full matches.
TODO: Scales which are not LCM but sound good (pentatonic, harmonic minor). 
Perhaps lcm 24 is too big, max might be 16 with 24 consisting of e.g. 8@0 + 12@0. 
This could mean the large isomorphic 5-member group is the largest lcm, with larger groups consisiting of superpositions of this group, functioning like modulating sets for melody.
15@0 is at best 10@0 + 12@0 which leaves key 9 out, perhaps why it lacks a consonant voicing
The voicings of larger groups spanning more than an octave might sound better or worse due to auxiliary effects e.g. lcm 24 might be perceived as multiple separate simultaneous chords.
* Pentatonic - melodies congruent with an 0 4 7 chord and the shared notes of its 3 possible lcm 24 interpretations at 0, 5, 7(lcm 24 is 8@0 + 8@5 = 8@0 + 12@0 produces overlapping families)
* Harmonic minor - construct from lcm 8 families?
* 0 3 7 6 - e.g. fur elise. Why does the 6 sound good?
TODO: Chord progressions
TODO: Triad experiment from ""Timbral effects on consonance disentangle psychoacoustic mechanisms and suggest perceptual origins for musical scales"" with a slider for overtone stretch and a two-dimensional surface where the user can move a mouse to adjust intervals (as in their study 5). This way a user can listen to triad chord consonance.
TODO: compare LCMs with modes - e.g. playing Mixolydian C is just playing Major scale F but using the C note as tonic.
 * True tonic is perhaps the reference point and playing music as if another note is the reference point produces a certain feel (probably juking/jiving/feinting/unexpected feelings).

Deferred: all of the below is sketchpad (moved from docs page)

TODO: example rhythms: 
* isochronic
* isochronic then adding in subdivisions
* Isochronic then adding in "superdivisions", making the original turn out to be a subdivision
* Removing beats to produce off beat syncopation
* Removing beats to produce groove
* Aksak and Jembe rhythms (non even vs. non isochronous?)

TODO: 
* investigate uniform chords - 12 tet can be divided in 2,3 and 4.
* bridge into melodies i.e. large sets might still be useful scales. 
* compare with harmonic minor scale - seems to be the lcm 15 flavor as if 15 is a bad subset of harmonic minor.
 * Research on why there are scales?
* perhaps start randomising chords and melody -> some notes sound bad, some note sets cause a certain flavour (even/odd/uniform)
 * Research on why there are chord progressions? Relation to melody? 

At this point we should make some notes on the validity of harmonic pattern theory - what exactly is it about?

Is it about consonance? 

TODO: tie in to consonance page.

If the hypotheses is that all wave patterns of sufficiently small size sound consonant or pleasing,
then the harsh sound of lcm 15 (and 20) either invalidate the hypothesis or implies that 15 is too large a WPL. 
Since lcm 18 and 24 sound (more) consonant a "smaller is better" rule is too simple.

Possible explanations include:

* **Pattern compression** - the large consonant LCMs contain a power of two, meaning their patterns past the halfway point can be inferred as mirrored repetitions.
The effective length of lcm 18 and 24 is then 9 and 12, below 15. In such a sense the "smaller is better" rule might hold.
* **Preference** - The brain might be partial for which patterns to match. It could prefer not only just small WPLs, but even WPLs, or specific LCMs. 
This could lead to situations where something could have been reconigzed as e.g. lcm 15 or 20,
but instead the brain tries to interpret them as a preferred LCM with some bad notes, leading to dissonance. 
(TODO: example competing LCMs with 15 and 20)
* **Symmetric Chords** - In 12-TET the chords 0 3 6 9 and 0 4 8 are **symmetric**, meaning there is no way apart from lowest/highest frequency to prefer a reference point.
Such chords have a distinct tension different from the dissonance in e.g. a semitone cluster. (TODO: example audio).
Symmetric chords are present in both lcm 15 and 20 and might contribute in some way to their dissonant voicings.


**Work in progress below**

Removing cluster notes yields gentler subsets:

**`15@0` = `{0 1 3 5 8 9 10}`**

- remove 8 → `{0 1 3 5 9 10}` (only superset `15@0`); best voicing `10 1 5 9 0 3` — good but
  tense/dramatic, note the high dim.
- remove 9 → `{0 1 3 5 8 10}` = `18@3`, a subset of `24@1` and `24@8`.
- remove 10 → `{0 1 3 5 8 9}` = `20@5`.

**`20@0` = `{0 3 4 7 8 10}`**

- remove 3 → `{0 4 7 8 10}` (superset `15@7`); best voicing `8 0 4 7 10` — quite tense.
- remove 4 → `{0 3 7 8 10}` = `8@8`, subset of `18@10`, `24@3`, `24@8`.
- remove 7 → `{0 3 4 8 10}` (superset `15@7`); best voicing `4 8 10 0 3` — pretty good,
  `8 10 0 3` is `5@0` with slight tension from key 4.
- remove 8 → `{0 3 4 7 10}` (superset `15@7`); best voicing `4 7 10 0 3` — okay but tense, a C7
  with an added key 3.

As expected, the smaller LCMs sound best, and under isomorphism the best voicings contain powers
of two. The hardest LCMs to voice consonantly are the large ones — especially those carrying the
relatively large prime 5.

## The curious case of LCM 24

The largest LCM, 24, is interesting: anecdotally it sounds *more* consonant when its dim chord is
mitigated rather than stated explicitly. Compare `5 9 0 4 7 11 2` with `5 9 0 4 7 11 2 5` — the
explicit repeated 5 adds harsh tension. Likewise `9 0 4 7 11 2 5` and `11 2 5 9 0 4 7` are harsh
with the dim exposed at the ends, whereas `7 11 2 5 9 0 4` embeds the dim inside a G7 that pulls
toward C major. Since C major is `4@0`, which divides `24@0`, there may be a chord-progression
mechanism at play.

It is notable that LCM 24 has consonant voicings while the smaller 20 and 15 do not. Possible
reasons:

- **Prime 5 destabilises large patterns**, perhaps via chunking (larger primes, larger chunks).
- **A preference for certain LCMs** — the brain may map wave packets to favoured LCMs, hearing
  `15@0` as `18@3` with a bad key 9, or `20@0` as `8@8` with a bad key 4.
- **Uniform chords.** When a chord's consecutive intervals divide the $k$-TET (aug `{0 4 8}`,
  dim7 `{0 3 6 9}` in 12-TET), the chord is ambiguous as to orientation and sounds tense, perhaps
  because the brain cannot parse it onto any single LCM. Aug appears only in LCM 15 and 20, always
  with a note in the semitone cluster; dim7 appears in none, but dim is in 15, 20, and 24.

:::note Harmonic minor aside
`15@0` = `{0 1 3 5 8 9 10}` is close to the A♯ harmonic minor scale `{0 1 3 5 6 9 10}` (key 8 for
6), which is close to the A♯ natural minor `{0 1 3 5 6 8 10}` = `24@1` (key 9 for 8). The harmonic
minor placed at 10 reads like `24@1` built on an aug rather than a major chord — a key set near
`24@1` with no full match of its own, containing both an aug (`@1,5,9`) and a dim7 (`@0,3,6,9`):
a very ambiguous set.
:::

## Chords and Melody

What drives chord progressions? Why does e.g. Dm-> D work? F -> A with A# melody?

A sounding chord can belong to many placements, and every key set with a full match is a subset
of `15` or `24`. Playing melody over chords can be seen as modulating between the placements the
chord allows.

The collapsed [LCM-families graph](../theory/lcm-families.mdx) shows that, with max size 24, max
prime 5, up to LCM 24, two supersets contain all other full matches. We call these the **maximal
LCM families**: a family is maximal iff it is never a proper subset of any other.

```bash
melodroid table chord-melody --chord-keys 0 4 7 --ktet 12
```

renders a matrix: rows are the maximal-LCM placements containing the chord (sorted by LCM then
anchor), columns are the $k$ keys, and a cell is `●` when the placement contains that key. The
chord-key columns are tinted green. To ask "which placements survive if I add key K as melody?",
read column K. For the C major triad `{0, 4, 7}` the rows are `15@4 15@7 15@11 24@0 24@5 24@7`;
column 5 is filled at `15@4 15@7 24@0 24@5` and blank at `15@11 24@7`, so playing key 5 keeps four
interpretations and rules out two.
### On a Sour Note (music/on-a-sour-note.mdx)

## Related Research
### Consonance (related-research/consonance.mdx)
#### Stretched overtones and intervals
#### Mixed timbres with a bonang

## CLI Reference
### Installation (cli/installation.mdx)
### CLI Reference (cli/reference.mdx)
### Wave Pattern Plots (cli/wave-pattern-plots.mdx)

# Claude
Claude MD probably needs an update

# General
Cleanup program for hints, e.g. collection initializations can be simplified (lots of oververbose calls)

# Octave sweep
## Animated octave sweep
Animated plot with play button and slider (for choosing sweep position) which shows the octave being swept, moving the reference point along good fractions with surrounding bins and noting when full matches occur
* Possibly interactive input of bin radius, updating the sweep