import type { KnownInlineScripts } from './known-inline-scripts';

/** How a rendered page's inline scripts are judged, and who hears of those left out. */
export interface ScriptPolicy {
  readonly known: KnownInlineScripts;
  /** Told how many inline scripts a page holds that the build never wrote. */
  readonly onUnexpected: (count: number) => void;
}
