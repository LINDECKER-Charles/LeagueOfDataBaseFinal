import type { CatalogImage } from '../../../core/api/generated/models/catalog-image';

/** Browser-ready URL of a catalogue image, or null when it holds none. */
export function imageSource(image: CatalogImage): string | null {
  return image.status === 'present' ? (image.url ?? null) : null;
}
