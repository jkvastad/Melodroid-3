// Sampled-piano note map for the optional "Piano" instrument, shared by the players
// (RhythmPatternPlayer, SequencePlayer). Salamander's native minor-third subset — A/C/D♯/F♯
// per octave across C2…C6 (see static/samples/piano/NOTICE.txt). Tone.Sampler picks the
// nearest sample and playback-rate-shifts it, so this spacing keeps the worst-case shift to
// ≤ ±1 semitone (vs ±6 for the old C-per-octave set). Keys are note names Tone parses (D#2),
// values the on-disk filenames (sharps are Ds/Fs). Fetched lazily on the first Play with Piano
// selected — nothing downloads for the sine default.
export const PIANO_URLS: Record<string, string> = {
  C2: 'C2.mp3', 'D#2': 'Ds2.mp3', 'F#2': 'Fs2.mp3', A2: 'A2.mp3',
  C3: 'C3.mp3', 'D#3': 'Ds3.mp3', 'F#3': 'Fs3.mp3', A3: 'A3.mp3',
  C4: 'C4.mp3', 'D#4': 'Ds4.mp3', 'F#4': 'Fs4.mp3', A4: 'A4.mp3',
  C5: 'C5.mp3', 'D#5': 'Ds5.mp3', 'F#5': 'Fs5.mp3', A5: 'A5.mp3',
  C6: 'C6.mp3',
};

// Site-relative sample directory; pass through Docusaurus's useBaseUrl() at the call site
// (it prepends the deploy baseUrl) before handing it to Tone.Sampler's baseUrl.
export const PIANO_SAMPLE_PATH = '/samples/piano/';
