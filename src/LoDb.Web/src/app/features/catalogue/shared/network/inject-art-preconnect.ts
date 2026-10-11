import { inject } from '@angular/core';
import type { ImageVariant } from '../../../../core/api/generated/models/image-variant';
import { PageResponse } from '../../../../core/routing/response/page-response';

function originOf(url: string): string | null {
  try {
    return new URL(url).origin;
  } catch {
    return null;
  }
}

/**
 * Connects early, from the response's headers, to the CDN a page's LCP art is hotlinked from:
 * only when the site stores no variant of it (an older patch, or the latest before its
 * variants are stored), the art then coming from there. To call from an injection context;
 * the function it returns takes the art's URL and its variants. Server side only.
 */
export function injectArtPreconnect(): (art: string, variants: readonly ImageVariant[]) => void {
  const response = inject(PageResponse);
  return (art, variants) => {
    const origin = variants.length === 0 ? originOf(art) : null;
    if (origin !== null) {
      response.preconnect(origin);
    }
  };
}
