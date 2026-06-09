import React from 'react';
import BrowserOnly from '@docusaurus/BrowserOnly';
import type {TimbreMorphPlayerProps} from './TimbreMorphPlayerClient';

// Like TimbrePlayer.tsx / Player.tsx: Tone.js touches browser-only APIs (window /
// AudioContext) that do not exist during Docusaurus's static (SSR) build.
// <BrowserOnly> defers the real slider-driven additive-synth player to the client so
// pages can use <TimbreMorphPlayer .../> directly.
export default function TimbreMorphPlayer(props: TimbreMorphPlayerProps) {
  return (
    <BrowserOnly
      fallback={
        <button className="button button--primary button--sm" disabled>
          {props.label ?? 'Play'}
        </button>
      }>
      {() => {
        const TimbreMorphPlayerClient =
          require('@site/src/components/TimbreMorphPlayerClient').default;
        return <TimbreMorphPlayerClient {...props} />;
      }}
    </BrowserOnly>
  );
}
