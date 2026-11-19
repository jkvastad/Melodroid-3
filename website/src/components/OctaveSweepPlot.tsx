import React from 'react';
import BrowserOnly from '@docusaurus/BrowserOnly';
import type {OctaveSweepPlotProps} from './OctaveSweepPlotClient';

// uPlot draws to a <canvas> and Tone.js touches the Web Audio API — neither exists during
// Docusaurus's static (SSR) build. <BrowserOnly> defers the real component to the client, so MDX
// pages can use <OctaveSweepPlot .../> directly without breaking the build — same pattern as
// WavePlot.tsx / Player.tsx.
export default function OctaveSweepPlot(props: OctaveSweepPlotProps) {
  return (
    <BrowserOnly
      fallback={
        <div
          style={{
            height: (props.height ?? 300) + 120,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            opacity: 0.5,
          }}>
          Loading octave sweep…
        </div>
      }>
      {() => {
        const OctaveSweepPlotClient =
          require('@site/src/components/OctaveSweepPlotClient').default;
        return <OctaveSweepPlotClient {...props} />;
      }}
    </BrowserOnly>
  );
}
