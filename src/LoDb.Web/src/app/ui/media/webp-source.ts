const SOURCE_EXTENSION = '.png';
const SIBLING_EXTENSION = '.webp';

/**
 * WebP twin of an ingested image. The storage writes a WebP next to every PNG, so the modern
 * candidate is derived from the original path rather than carried in the payload; anything
 * but a PNG has no twin, and the browser is never pointed at a file that was never written.
 */
export function webpSource(source: string): string | null {
  if (!source.endsWith(SOURCE_EXTENSION)) {
    return null;
  }
  return source.slice(0, -SOURCE_EXTENSION.length) + SIBLING_EXTENSION;
}
