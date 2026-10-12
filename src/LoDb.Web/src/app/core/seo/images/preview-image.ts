import type { SeoImage } from './seo-image';

// Every card of public/preview/ is drawn at the size Open Graph recommends.
const PREVIEW_WIDTH = 1200;
const PREVIEW_HEIGHT = 630;

/** A section of the site that has a preview card of its own. */
export type PreviewSection = 'home' | 'champions' | 'items' | 'runes' | 'summoners';

/** The site's card of a section, `/preview/{section}.png`, with its size. */
export function previewImage(section: PreviewSection): SeoImage {
  return { url: `/preview/${section}.png`, width: PREVIEW_WIDTH, height: PREVIEW_HEIGHT };
}
