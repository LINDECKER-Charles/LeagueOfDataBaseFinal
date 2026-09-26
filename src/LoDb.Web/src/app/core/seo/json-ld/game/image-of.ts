import type { CatalogImage } from '../../../api/generated/models/catalog-image';
import type { SeoUrls } from '../../urls/seo-urls';

/** The absolute URL of a catalog image, or null for a placeholder. */
export function imageOf(image: CatalogImage, urls: SeoUrls): string | null {
  return image.url ? urls.absolute(image.url) : null;
}
