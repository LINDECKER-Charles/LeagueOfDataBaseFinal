import type { TokenBucket } from './token-bucket';

/** What the admission remembers of one client (`ClientTable`). */
export interface ClientState {
  /** Renders of this client running now. */
  inFlight: number;
  /** Its own budget of renders of a pinned version. */
  readonly pinnedBudget: TokenBucket;
}
