# Docs
Can't find npm or node via terminal in vs code, something went wrong during node install.
How do i spin up the node server for local testing?
Needs general fixing, several bugs and uglies

## intro.mdx
### Harmony as pattern recognition of simultaneous waves
$\{1,\ 5/4,\ 3/2\}$ shows ups as {1, 5/4, 3/2}{1, 5/4, 3/2} - one in katex format and the other in plain text. Same for 

$\{p_1/q_1,\ \ldots,\ p_n/q_n\}$

and

$$
\mathrm{WPL} = \operatorname{lcm}(q_1,\ q_2,\ \ldots,\ q_n).
$$


---
Mermaid graph in LCM families is kind of big and gets compressed, perhaps if it can be large and scrolled around?
LCM 8 under docs/theory/lcm-families#how-the-families-relate produces audible click

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