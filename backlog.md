# Docs
Needs general fixing, several bugs and uglies. Sections below mirror the website doc structure (website/docs/).

## Intro (intro.mdx)
Mermaid graph in LCM families is kind of big and gets compressed, perhaps if it can be large and scrolled around?

## Theory
### Good Fractions (theory/good-fractions.mdx)
### LCM Families (theory/lcm-families.mdx)
LCM 8 produces audible click
### Wave Pattern Plots (theory/wave-pattern-plots.mdx)
### Cluster Ranges (theory/cluster-ranges.mdx)
### Playing Fractions (theory/playing-fractions.mdx)

## The Keyboard
### A Good Keyboard (keyboard/a-good-keyboard.mdx)
### Key Sweep (keyboard/key-sweep.mdx)

## The Sound of Music
### Voicings and Placements (music/voicings-and-placements.mdx)
### Voicings and LCM Families (music/voicings-and-lcm-families.mdx)
### On a Sour Note (music/on-a-sour-note.mdx)

## CLI Reference (cli/reference.mdx)

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