# Octave sweep
## Full Matches
Currently there are a lot of full matches for "the same match". Sometimes there is an obvious best match where all renormalized ratios have 0 distance from their binned good fractions (a perfect full match), but theoretically there could be matches where some are closer than others or there might be a perfect match but it is not on any sweep step. Generally full matches seem to produce intervals around a center sweep step where all consecutive steps produce full matches - perhaps this center step could be thought of as the "true" full match, or just simply call the full match centered.

TODO: An option to show only centered full matches. 
TODO: Since the project is exploratory it is probably best to show ambigous matches along with full matches, since they technically are full matches - they just dont have a well defined lcm. 

Since centered full matches convey the idea of tuning our given ratios to our good fractions, sort of like tuning into a radio station, we might run into a problem where two tunings compete - e.g. overlapping or directly adjacent bins. This could produce sweep steps with full matches for one centered match overlapping with sweep steps for another, making it technically one centered match but missing the conceptual point (e.g. different "radio stations", even differing lcms within the same full match block).

### Better discovery for full matches
Currently if the bin radius is too small (e.g. default 0.006211) some important potential full matches are missed, e.g. {1.0 1.25 1.33 1.5 1.8} which matches 24 at 1.33 for slightly higher bin radius c (how high?). Perhaps some kind of bootstrap algorithm would be nice, where we start with high bin radius and look for full matches. This produces some amount of centered full matches. Then we lower in radius in increments and note where full matches dissapear. 

## Ambigous matches
For some bin widths matches will be ambigous. E.g. full match only option  prints only full matches, how to handle ambigous matches there?

## Animated octave sweep
Animated plot with play button and slider (for choosing sweep position) which shows the octave being swept, moving the reference point along good fractions with surrounding bins and noting when full matches occur
* Possibly interactive input of bin radius, updating the sweep