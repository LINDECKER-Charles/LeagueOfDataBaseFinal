import type { Response } from 'express';
import type { RenderLane } from './render-lane';
import { renderLaneOf } from './render-lane-of';
import type { RenderLimits } from './render-limits';
import { TokenBucket } from './token-bucket';

const SERVICE_UNAVAILABLE = 503;
// Long enough for a crawler to back off, short enough for a visitor's reload.
const RETRY_AFTER_SECONDS = '30';

function refuse(response: Response): void {
  response.set({ 'Cache-Control': 'no-store', 'Retry-After': RETRY_AFTER_SECONDS });
  response.status(SERVICE_UNAVAILABLE).type('text/plain').send('Service Unavailable');
}

/**
 * Starts only the renders the server has room for and answers the others a 503 at once.
 * Without it, a crawl of every older patch in every language queued renders until each page
 * outlasted the crawler's patience: it gave up and asked again, while the renderer and the
 * API kept working for nobody and the whole host slowed down.
 *
 * Pinned versions render within a budget of their own (rate and slots); the current pages
 * keep the remaining slots. A slot is held until the render settles, not until the client
 * leaves, since a render whose client is gone still costs its CPU. Over a 503, nginx serves
 * the stale copy it may hold (`proxy_cache_use_stale`).
 */
export class RenderAdmission {
  private readonly inFlight: Record<RenderLane, number> = { pinned: 0, current: 0 };
  private readonly pinnedBudget: TokenBucket;

  constructor(
    private readonly limits: RenderLimits,
    now?: () => number,
  ) {
    this.pinnedBudget = new TokenBucket(limits.pinnedPerMinute, limits.pinnedBurst, now);
  }

  /** Runs `render` for the root-relative `url` when admitted, else answers the 503. */
  async run(url: string, response: Response, render: () => Promise<void>): Promise<void> {
    const lane = renderLaneOf(url);
    if (!this.admits(lane)) {
      refuse(response);
      return;
    }
    this.inFlight[lane] += 1;
    try {
      await render();
    } finally {
      this.inFlight[lane] -= 1;
    }
  }

  // The token is spent last: a render refused for want of a slot keeps the budget intact.
  private admits(lane: RenderLane): boolean {
    if (this.inFlight.pinned + this.inFlight.current >= this.limits.maxInFlight) {
      return false;
    }
    if (lane === 'current') {
      return true;
    }
    return this.inFlight.pinned < this.limits.pinnedMaxInFlight && this.pinnedBudget.tryTake();
  }
}
