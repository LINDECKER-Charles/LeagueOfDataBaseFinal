import type { ImageVariant } from '../../../core/api/generated/models/image-variant';

/**
 * The `srcset` of an art's stored WebP variants, each with its `w` descriptor; null without
 * any (an older patch, or the latest before its variants are stored), the hotlinked art then
 * standing alone.
 */
export function srcsetOf(variants: readonly ImageVariant[]): string | null {
  if (variants.length === 0) {
    return null;
  }
  return variants.map((variant) => `${variant.url} ${variant.width}w`).join(', ');
}
