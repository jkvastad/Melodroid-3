# Docs
* Note that there is a high severity vulnerability in serialize-javascript <=7.0.4 which is a false alarm because the only input which could be rce is on build side. Just wait for docusaurus to patch it out. Do not npm audit fix --force, which would attempt a breaking major upgrade.

Needs general fixing, several bugs and uglies. Sections below mirror the website doc structure (website/docs/).

Mixed usage of katex for e.g. hz - sometimes inline mathrm, sometimes just plain text.

## Intro (intro.mdx)
Perhaps playing a sound should not be infinite duration per default?

## Theory
Perhaps the docusaurus default boxed layout of subsections should instead be a list as sections build on each other top to bottom?
### Good Fractions (theory/good-fractions.mdx)
Weird styling on the G13/Cmaj13 part, G is italicized but not the Cmaj
### LCM Families (theory/lcm-families.mdx)
### Cluster Ranges (theory/cluster-ranges.mdx)
### Playing Fractions (theory/playing-fractions.mdx)

## The Keyboard
### A Good Keyboard (keyboard/a-good-keyboard.mdx)
### Key Sweep (keyboard/key-sweep.mdx)

## The Sound of Music
TODO: discuss consonance in relation to timbre (and possibly encultured preference). 
* What is consonance? (pleasantness)
* Does it matter that lcms 15/20 do not sound consonant?
* Try playing lcms and fractions with different overtone series. Is it possible to construct series so that certain fractions/lcms sound bad/good depending on overtones?
TODO: section discussing composition - meoldy and chord progressions via full matches.
### Voicings and Placements (music/voicings-and-placements.mdx)
### Voicings and LCM Families (music/voicings-and-lcm-families.mdx)
### On a Sour Note (music/on-a-sour-note.mdx)

## Related Research
### Related Research (related-research/related-research.mdx)

## CLI Reference
### Installation (cli/installation.mdx)
### CLI Reference (cli/reference.mdx)
### Wave Pattern Plots (cli/wave-pattern-plots.mdx)

# Claude
Update claude.md to reflect the new github page docs section structure replacing the old readme style.
Claude MD probably needs an update after the docs rework


# The sound of music
Web app, perhaps github pages, where user can listen to proc. gen. music based on theory. Possibly use Tone.js vs. just creating a custom synth. Possibly work by generating a MIDI which is played in browser, and user can download it for inspection if something interesting happened.

## Deferred
Investigate the sound of all basic lcms.
Investigate sound of key sets with no small lcm which sound good, primarily the harmonic minor which is close to lcm 15 (with reference point at key 0 swap out key 8 for key 6).

# General
Cleanup program for hints, e.g. collection initializations can be simplified (lots of oververbose calls)


# Octave sweep
## Animated octave sweep
Animated plot with play button and slider (for choosing sweep position) which shows the octave being swept, moving the reference point along good fractions with surrounding bins and noting when full matches occur
* Possibly interactive input of bin radius, updating the sweep