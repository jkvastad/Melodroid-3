// Sampled-piano note map for the optional "Piano" instrument, shared by the players
// (RhythmPatternPlayer, SequencePlayer). A small C-per-octave subset of the Salamander
// Grand Piano (see static/samples/piano/NOTICE.txt); Tone.Sampler pitch-shifts these
// across the keyboard, so a few files cover the whole range. Fetched lazily on the first
// Play with Piano selected — nothing downloads for the sine default.
export const PIANO_URLS: Record<string, string> = {
  C2: 'C2.mp3',
  C3: 'C3.mp3',
  C4: 'C4.mp3',
  C5: 'C5.mp3',
  C6: 'C6.mp3',
};

// Site-relative sample directory; pass through Docusaurus's useBaseUrl() at the call site
// (it prepends the deploy baseUrl) before handing it to Tone.Sampler's baseUrl.
export const PIANO_SAMPLE_PATH = '/samples/piano/';
