### The sound of music
table family-overlap
* Columns lack names now, should be "Key, Placement, Overlap"
* Green row if full overlap
table key-supersets
* Extra column can show the actual extra keys rather than the number of extra keys

Update README to reflect changes.

---Deferred:

Investigate the sound of all basic lcms.
Investigate sound of key sets with no small lcm which sound good, primarily the harmonic minor which is close to lcm 15 (with reference point at key 0 swap out key 8 for key 6).

# General
Cleanup program for hints, e.g. collection initializations can be simplified (lots of oververbose calls)

# Key Sweep
Max LCM option to only show rows with LCM below a certain threshold

# Octave sweep
## Animated octave sweep
Animated plot with play button and slider (for choosing sweep position) which shows the octave being swept, moving the reference point along good fractions with surrounding bins and noting when full matches occur
* Possibly interactive input of bin radius, updating the sweep