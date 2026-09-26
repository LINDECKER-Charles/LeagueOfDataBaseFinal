import type { HomeLink } from './home-link';

/** A card of a preview, ready to render. */
export interface PreviewCard {
  readonly link: HomeLink;
  readonly name: string;
  readonly caption: string;
  /** Browser-ready URL of the image, null when the catalogue holds none. */
  readonly image: string | null;
}
