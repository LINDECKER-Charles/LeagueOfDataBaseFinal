import type { CatalogImage } from '../../../core/api/generated/models/catalog-image';

/** One entry of a preview, as the list endpoint gives it. */
export interface PreviewEntry {
  /** The API's `canonicalPath`, below `/{locale}/[{version}/]`. */
  readonly path: string;
  readonly name: string;
  /** The line under the name: a champion's title, an item's id, a rune path's key. */
  readonly caption: string;
  readonly image: CatalogImage;
}
