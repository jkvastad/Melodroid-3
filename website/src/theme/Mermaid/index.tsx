import React, {useEffect, useRef} from 'react';
import Mermaid from '@theme-original/Mermaid';
import type MermaidType from '@theme/Mermaid';
import type {WrapperProps} from '@docusaurus/types';

type Props = WrapperProps<typeof MermaidType>;

// The original Mermaid theme component renders a static inline SVG with no zoom
// or pan, which makes busy graphs (e.g. the LCM-families relation graph) hard to
// read. This wrapper attaches svg-pan-zoom to the rendered SVG so readers can
// wheel-zoom and drag-pan in place. svg-pan-zoom touches the DOM and is required
// only inside the effect, so it never runs during SSR.
export default function MermaidWrapper(props: Props): React.ReactElement {
  const containerRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    let instance: {destroy?: () => void} | undefined;

    const attach = (): boolean => {
      const svg = container.querySelector('svg');
      if (!svg) return false;

      // Mermaid sizes the SVG to its content with a max-width; give it a stable
      // viewport box so svg-pan-zoom has concrete dimensions to fit/center into.
      svg.style.maxWidth = '100%';
      svg.style.width = '100%';
      svg.style.height = '70vh';

      const svgPanZoom = require('svg-pan-zoom');
      instance = svgPanZoom(svg, {
        zoomEnabled: true,
        panEnabled: true,
        controlIconsEnabled: true, // on-screen +/−/reset, so wheel isn't the only way
        mouseWheelZoomEnabled: true,
        fit: true,
        center: true,
        minZoom: 0.5,
        maxZoom: 20,
      });
      return true;
    };

    // theme-mermaid renders the SVG asynchronously (mermaid.render is async), so
    // it may not be present on first paint — retry via a MutationObserver until
    // it appears, then stop watching.
    if (attach()) return () => instance?.destroy?.();

    const observer = new MutationObserver(() => {
      if (attach()) observer.disconnect();
    });
    observer.observe(container, {childList: true, subtree: true});

    return () => {
      observer.disconnect();
      instance?.destroy?.();
    };
  }, []);

  return (
    <div>
      <div ref={containerRef}>
        <Mermaid {...props} />
      </div>
      <div style={{fontSize: '0.8rem', opacity: 0.7, marginTop: '0.25rem'}}>
        drag to pan · scroll to zoom · ⟲ reset
      </div>
    </div>
  );
}
