import React from 'react';
import BrowserOnly from '@docusaurus/BrowserOnly';
import type {RhythmPatternPlayerProps} from './RhythmPatternPlayerClient';

// Tone.js and uPlot both touch browser-only APIs (AudioContext, canvas sizing) that
// do not exist during Docusaurus's static (SSR) build. <BrowserOnly> defers the real
// widget to the client, so pages can drop in <RhythmPatternPlayer .../> directly.
// Mirrors SequencePlayer.tsx / Player.tsx.
export default function RhythmPatternPlayer(props: RhythmPatternPlayerProps) {
  return (
    <BrowserOnly
      fallback={
        <button className="button button--primary button--sm" disabled>
          Play
        </button>
      }>
      {() => {
        const RhythmPatternPlayerClient =
          require('@site/src/components/RhythmPatternPlayerClient').default;
        return <RhythmPatternPlayerClient {...props} />;
      }}
    </BrowserOnly>
  );
}
