/**
 * A path segment percent-decoded, as the router reads it, or null when an escape is
 * malformed (`%E0%A4%A`). The one decoder of the URL classification: the server and the app
 * must see `/%66r/%31%35.13.1/` as the `fr` locale and the `15.13.1` version alike.
 */
export function decodeSegment(segment: string): string | null {
  try {
    return decodeURIComponent(segment);
  } catch {
    return null;
  }
}
