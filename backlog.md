# General
* chord-melody could perhaps be used with a programmable LED keyboard to show which scales are available from pressed keys. 
As a POC we could have a virtual programmable LED keyboard which shows a piano roll on the screen, reads midi input and lights up keys, so the player can watch the screen while playing if there is no physical LED keyboard.

# Docs
* Note that there is a high severity vulnerability in serialize-javascript <=7.0.4 which is a false alarm because the only input which could be rce is on build side. Just wait for docusaurus to patch it out. Do not npm audit fix --force, which would attempt a breaking major upgrade.

* Does preference for small primes tie in with musical neurodynamics?

* section on pragmatic music i.e. 
 * chord melody - what keys imply what perceptions based on current chord?
 * Tiling and recreation - can we find the patterns of a song and then randomly draw from that pattern to produce a similar feel?

* Update backlog Docs headers

## Related Research - Consonance (related-research/consonance.mdx)
TODO: Triad experiment from ""Timbral effects on consonance disentangle psychoacoustic mechanisms and suggest perceptual origins for musical scales"" with a slider for overtone stretch and a two-dimensional surface where the user can move a mouse to adjust intervals (as in their study 5). This way a user can listen to triad chord consonance.

## CLI Reference (cli/reference.mdx)
Mostly slop docs, needs a human touch to be truly understandable

# Claude
Claude MD probably needs an update