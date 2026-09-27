import type { Previews } from './previews';

/** The four previews as read, and when to read them again, if the API asked for it. */
export interface FetchedPreviews {
  readonly previews: Previews;
  /** The longest `Retry-After` of the four lists: a cold version's images are on their way. */
  readonly retryAfterMs: number | null;
}
