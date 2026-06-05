import React from 'react';
import BrowserOnly from '@docusaurus/BrowserOnly';
import type {SequencePlayerProps} from './SequencePlayerClient';

// Like Player.tsx: Tone.js touches browser-only APIs (window / AudioContext) that
// do not exist during Docusaurus's static (SSR) build. <BrowserOnly> defers the
// real player to the client so pages can use <SequencePlayer .../> directly.
export default function SequencePlayer(props: SequencePlayerProps) {
  return (
    <BrowserOnly
      fallback={
        <button className="button button--primary button--sm" disabled>
          {props.label ?? 'Play'}
        </button>
      }>
      {() => {
        const SequencePlayerClient =
          require('@site/src/components/SequencePlayerClient').default;
        return <SequencePlayerClient {...props} />;
      }}
    </BrowserOnly>
  );
}
