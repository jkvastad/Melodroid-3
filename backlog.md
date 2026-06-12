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
Some notes on consonance: Seems like there is some work based on dissonance curves for all overtones (the spectrum) of an instrument, from which we can do calculations on which intervals minimize the global roughness from these dissonances.
TODO: go through the experiments of "Timbral effects on consonance disentangle psychoacoustic mechanisms and suggest perceptual origins for musical scales"
* Claude dropped the ball on cases but can still be salvaged, created useful tools
* Perhaps go through all experiments and create interactive graphs to show the most salient points?
* Summarise with implications for WPL based theory - why does e.g. harmonic minor sound good but not lcm 15?
TODO: Also check out "Consonance and Pitch" which looks into how expectation affects consonance
TODO: discuss consonance in relation to timbre (and possibly encultured preference). 
* What is consonance? (pleasantness)
* Does it matter that lcms 15/20 do not sound consonant?
* Try playing lcms and fractions with different overtone series. Is it possible to construct series so that certain fractions/lcms sound bad/good depending on overtones?
 * Add an auto marker which shows the best harmonics blend? Is there a spot for a given ratio and harmonics where timbres are "best"?
 * Try playing a three tone chord with ratio powers
TODO: section discussing composition - meoldy and chord progressions via full matches.
### Voicings and Placements (music/voicings-and-placements.mdx)
### Voicings and LCM Families (music/voicings-and-lcm-families.mdx)
TODO: to align cases with experiments from "Timbral effects on consonance..." case A should not only allow stretch but also allow a semitone slider from 11 to 13 semitones, effectively merging case B into case A.
TODO: Case C is interesting in and of itself but not related to the paper. Could be expanded into playing melodies with dynamically auto-adjusting timbres.
DONE (related-research/consonance.mdx — "Mixed timbres with a bonang"): Add the bonang case with the fixed frequencies 𝑓0,1.52⁢𝑓0,3.46⁢𝑓0, and 3.92⁢𝑓0, for the upper tone and "the lower tone corresponds to a standard harmonic tone with four equally weighted harmonics.". 
This allows the user to sweep across the semitones and note for themselves where consonance arrises.
TODO: Triad experiment with a slider for overtone stretch and a two-dimensional surface where the user can move a mouse to adjust intervals (as in their study 5). This way a user can listen to triad chord consonance.

Perhaps the lots-stone reference is not perfect as we are not really interested in their result but rather the background for helmholtz dyads they discuss.
### On a Sour Note (music/on-a-sour-note.mdx)

## Related Research
### Consonance (related-research/consonance.mdx)
Open up with the excellent review "Musical consonance: a review of theory and evidence on perception and preference of auditory roughness in humans and other animals" which discusses different models of consonance.
Move a lot of the consonance discussion from "Voicings and LCM Families" here, especially:
* The discussion of overtones vs. harmonicity for dyads in Timbral effects on consonance disentangle psychoacoustic mechanisms and suggest perceptual origins for musical scales
* Expectation paper "Consonance and pitch" 
* Enculturation "Indifference to dissonance in native Amazonians reveals cultural variation in music perception" and counter article 

## CLI Reference
### Installation (cli/installation.mdx)
### CLI Reference (cli/reference.mdx)
New superposition command - skips aliases for placements, might later on want to check e.g. "only allow one reference point"
TODO: superposition command deprecates key-supersets command, can be recreated with command options.
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