import React from 'react';
import BrowserOnly from '@docusaurus/BrowserOnly';
import type {TimbrePlayerProps} from './TimbrePlayerClient';

// Like Player.tsx / SequencePlayer.tsx: Tone.js touches browser-only APIs
// (window / AudioContext) that do not exist during Docusaurus's static (SSR)
// build. <BrowserOnly> defers the real additive-synth player to the client so
// pages can use <TimbrePlayer .../> directly.
export default function TimbrePlayer(props: TimbrePlayerProps) {
  return (
    <BrowserOnly
      fallback={
        <button className="button button--primary button--sm" disabled>
          {props.label ?? 'Play'}
        </button>
      }>
      {() => {
        const TimbrePlayerClient =
          require('@site/src/components/TimbrePlayerClient').default;
        return <TimbrePlayerClient {...props} />;
      }}
    </BrowserOnly>
  );
}
