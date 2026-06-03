// svg-pan-zoom ships no bundled TS types and @types/svg-pan-zoom is stale; we
// only use a tiny surface (the factory + instance.destroy), so an ambient
// declaration is enough.
declare module 'svg-pan-zoom';
