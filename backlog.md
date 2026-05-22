# General
Cleanup program for hints, e.g. collection initializations can be simplified (lots of oververbose calls)

# Octave sweep
## Full Matches
Strange looking readout after ambigous printout for 

dotnet run -- table octave-sweep --ratios 1.0, 1.25, 1.5 --bin-radius 0.01 --only-full-matches
   Ref │      1/1 │    16/15 │      10/9 │      9/8 │      6/5 │      5/4 │      4/3 │      3/2 │      8/5 │      5/3 │      16/9 │      9/5 │     15/8 │ LCM │ Full?
1.1140 │          │          │ ~+0.987 % │          │          │          │ +0.987 % │          │          │          │ ~+0.987 % │          │          │     │   ?  
The full match att 1.1140 looks like it just a regular full match with lcm 9, but it is flagged yellow as if ambigous?

### Competing Centered Full Matches
Since centered full matches convey the idea of tuning our given ratios to our good fractions, sort of like tuning into a radio station, we might run into a problem where two tunings compete - if a centered full match has an associated interval, two centered full matches might have directly adjacent intervals, producing one large interval with one resulting centered full match. Such a competing block requires adjacent sweep steps producing full matches with differing sets of good fractions (else it would just be the same interval) and so might be distinguished based on the good fractions present in the interval.

### Better discovery for full matches
Currently if the bin radius is too small (e.g. default 0.006211) some important potential full matches are missed, e.g. {1.0 1.25 1.33 1.5 1.8} which matches 24 at 1.33 for slightly higher bin radius c (how high?). Perhaps some kind of bootstrap algorithm would be nice, where we start with high bin radius and look for full matches. This produces some amount of centered full matches. Then we lower in radius in increments and note where full matches dissapear. 

## Animated octave sweep
Animated plot with play button and slider (for choosing sweep position) which shows the octave being swept, moving the reference point along good fractions with surrounding bins and noting when full matches occur
* Possibly interactive input of bin radius, updating the sweep