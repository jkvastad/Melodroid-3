import React from 'react';
import BrowserOnly from '@docusaurus/BrowserOnly';
import type {WavePlotProps} from './WavePlotClient';

// uPlot draws to a <canvas> and touches the DOM, which does not exist during
// Docusaurus's static (SSR) build. <BrowserOnly> defers the real plot to the
// client, so MDX pages can use <WavePlot .../> directly without breaking the
// build — same pattern as Player.tsx.
export default function WavePlot(props: WavePlotProps) {
  return (
    <BrowserOnly
      fallback={
        <div
          style={{
            height: props.height ?? 360,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            opacity: 0.5,
          }}>
          Loading plot…
        </div>
      }>
      {() => {
        const WavePlotClient =
          require('@site/src/components/WavePlotClient').default;
        return <WavePlotClient {...props} />;
      }}
    </BrowserOnly>
  );
}
