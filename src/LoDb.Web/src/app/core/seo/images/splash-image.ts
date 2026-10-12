import type { SeoImage } from './seo-image';

// Data Dragon draws every classic splash art at this size.
const SPLASH_WIDTH = 1215;
const SPLASH_HEIGHT = 717;

/**
 * A champion's splash art as the preview of its page: wide enough for a large card, and the
 * champion's own, where every champion page used to share the section's card.
 */
export function splashImage(url: string, alt: string): SeoImage {
  return { url, width: SPLASH_WIDTH, height: SPLASH_HEIGHT, alt };
}
