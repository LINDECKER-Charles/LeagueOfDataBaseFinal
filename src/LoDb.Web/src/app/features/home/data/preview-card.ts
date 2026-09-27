import type { CatalogImage } from '../../../core/api/generated/models/catalog-image';
import type { Edition } from '../../../core/api/generated/models/edition';
import type { HomeLink } from './home-link';

/** A card of a preview, ready to render. */
export interface PreviewCard {
  readonly link: HomeLink;
  readonly name: string;
  readonly caption: string;
  /** The image as the API describes it: a pending one sweeps until the home reads it again. */
  readonly image: CatalogImage;
  readonly edition: Edition;
  /** The tall art of a portrait card, a champion's loading screen; null for the others. */
  readonly art: string | null;
}
