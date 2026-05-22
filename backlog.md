# Octave sweep
There is a minor problem with the printout. When binning to good fraction 1, the value being binned will eventually fall below the reference point and get renormalized to a ratio closer to two. When this happens the distance to the reference point suddenly becomes large. This is an artifact - the distance is large if going on way, but small going the other: bins can fall out of the [1,2) range.

# Animated octave sweep
Animated plot with play button and slider (for choosing sweep position) which shows the octave being swept, moving the reference point along good fractions with surrounding bins and noting when full matches occur
* Possibly interactive input of bin radius, updating the sweep