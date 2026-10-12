/**
 * An Open Graph image: where it is and, when they are known, its size and what it shows, so
 * that a link preview reserves its room and a screen reader can tell it.
 */
export interface SeoImage {
  /** Root-relative or absolute. */
  readonly url: string;
  /** In pixels, when known: the section previews and Data Dragon's splash art. */
  readonly width?: number;
  readonly height?: number;
  /** What the image shows, translated; the page's title when left out. */
  readonly alt?: string;
}
