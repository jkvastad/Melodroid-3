# Docs
* Note that there is a high severity vulnerability in serialize-javascript <=7.0.4 which is a false alarm because the only input which could be rce is on build side. Just wait for docusaurus to patch it out. Do not npm audit fix --force, which would attempt a breaking major upgrade.

Needs general fixing, several bugs and uglies. Sections below mirror the website doc structure (website/docs/).

Mixed usage of katex for e.g. hz - sometimes inline mathrm, sometimes just plain text.

## Intro (intro.mdx)

## Theory
Perhaps the docusaurus default boxed layout of subsections should instead be a list as sections build on each other top to bottom?
### Good Fractions (theory/good-fractions.mdx)
### LCM Families (theory/lcm-families.mdx)
### Cluster Ranges (theory/cluster-ranges.mdx)
### Playing Fractions (theory/playing-fractions.mdx)
## The Keyboard
### A Good Keyboard (keyboard/a-good-keyboard.mdx)
### Key Sweep (keyboard/key-sweep.mdx)

## The Sound of Music

### Voicings and Placements (music/voicings-and-placements.mdx)
### Voicings and LCM Families (music/voicings-and-lcm-families.mdx)
TODO: Scales which are not LCM but sound good (pentatonic, harmonic minor)
TODO: Chord progressions
TODO: Triad experiment from ""Timbral effects on consonance disentangle psychoacoustic mechanisms and suggest perceptual origins for musical scales"" with a slider for overtone stretch and a two-dimensional surface where the user can move a mouse to adjust intervals (as in their study 5). This way a user can listen to triad chord consonance.
TODO: compare LCMs with modes - e.g. playing Mixolydian C is just playing Major scale F but using the C note as tonic.
 * True tonic is perhaps the reference point and playing music as if another note is the reference point produces a certain feel (probably juking/jiving/feinting/unexpected feelings).
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