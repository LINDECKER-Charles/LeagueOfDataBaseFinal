import type { TokenBucket } from './token-bucket';
import type { VisitorLane } from './visitor-lane';

/** What the admission remembers of one client (`ClientTable`). */
export interface ClientState {
  /** Renders of this client running now. */
  inFlight: number;
  /** Its own budget of renders in each lane, apart from the lane's budget for every client. */
  readonly budgets: Readonly<Record<VisitorLane, TokenBucket>>;
}
