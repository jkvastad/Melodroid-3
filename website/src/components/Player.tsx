import React from 'react';
import BrowserOnly from '@docusaurus/BrowserOnly';
import type {VoicingPlayerProps} from './VoicingPlayer';

// Tone.js touches browser-only APIs (window / AudioContext), which do not exist
// during Docusaurus's static (SSR) build. <BrowserOnly> defers the real player
// to the client, so pages can use <Player .../> directly without breaking the
// build.
export default function Player(props: VoicingPlayerProps) {
  return (
    <BrowserOnly
      fallback={
        <button className="button button--primary button--sm" disabled>
          {props.label ?? 'Play'}
        </button>
      }>
      {() => {
        const VoicingPlayer =
          require('@site/src/components/VoicingPlayer').default;
        return <VoicingPlayer {...props} />;
      }}
    </BrowserOnly>
  );
}
