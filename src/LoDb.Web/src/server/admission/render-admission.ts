import type { Response } from 'express';
import { ClientTable } from './client-table';
import type { ClientState } from './client-state';
import type { RenderCounts } from './render-counts';
import type { RenderLane } from './render-lane';
import { renderLaneOf } from './render-lane-of';
import type { RenderLimits } from './render-limits';
import type { RenderRefusal } from './render-refusal';
import type { RenderRequest } from './render-request';
import { SHED_MARKER } from './shed-marker';
import { TokenBucket } from './token-bucket';

const SERVICE_UNAVAILABLE = 503;
// Long enough for a crawler to back off, short enough for a visitor's reload.
const RETRY_AFTER_SECONDS = '30';

function refuse(response: Response): void {
  response.locals[SHED_MARKER] = true;
  response.set({ 'Cache-Control': 'no-store', 'Retry-After': RETRY_AFTER_SECONDS });
  response.status(SERVICE_UNAVAILABLE).type('text/plain').send('Service Unavailable');
}

/**
 * Starts only the renders the server has room for and answers the others a 503 at once.
 * Without it, a crawl of every older patch in every language queued renders until each page
 * outlasted the crawler's patience: it gave up and asked again, while the renderer and the
 * API kept working for nobody and the whole host slowed down.
 *
 * Every lane has a budget of renders per minute: pinned versions a small one and slots of
 * their own, the current pages a larger one, the guard of the host when many clients each
 * ask little. Every client has its own share: renders at once, and pinned renders per
 * minute, so that one client alone never takes what the others need. A slot is held until
 * the render settles, not until the client leaves, since a render whose client is gone still
 * costs its CPU. Over a 503, nginx serves the stale copy it may hold
 * (`proxy_cache_use_stale`).
 */
export class RenderAdmission {
  private readonly inFlight: Record<RenderLane, number> = { pinned: 0, current: 0 };
  private readonly laneBudgets: Readonly<Record<RenderLane, TokenBucket>>;
  private readonly clients: ClientTable;

  constructor(
    private readonly limits: RenderLimits,
    private readonly counts: RenderCounts,
    now?: () => number,
  ) {
    this.laneBudgets = {
      pinned: new TokenBucket(limits.pinnedPerMinute, limits.pinnedBurst, now),
      current: new TokenBucket(limits.currentPerMinute, limits.currentBurst, now),
    };
    const perClient = limits.pinnedPerClientPerMinute;
    const budgetOf = () => new TokenBucket(perClient, perClient, now);
    this.clients = new ClientTable(limits.trackedClients, budgetOf);
  }

  /** Runs `render` for the request when admitted, else answers the 503. */
  async run(
    request: RenderRequest,
    response: Response,
    render: () => Promise<void>,
  ): Promise<void> {
    const lane = renderLaneOf(request.url);
    const client = this.clients.stateOf(request.client);
    const refusal = this.refusalOf(lane, client);
    this.counts.record(lane, refusal);
    if (refusal !== null) {
      refuse(response);
      return;
    }
    this.admit(lane, client);
    try {
      await render();
    } finally {
      this.inFlight[lane] -= 1;
      client.inFlight -= 1;
    }
  }

  // Slots first, budgets last, the client's before the lane's: a client over its own share
  // leaves the lane's budget to the others.
  private refusalOf(lane: RenderLane, client: ClientState): RenderRefusal | null {
    const { limits } = this;
    if (this.inFlight.pinned + this.inFlight.current >= limits.maxInFlight) {
      return 'capacity';
    }
    if (client.inFlight >= limits.perClientMaxInFlight) {
      return 'client_capacity';
    }
    if (lane === 'pinned' && this.inFlight.pinned >= limits.pinnedMaxInFlight) {
      return 'lane_capacity';
    }
    if (lane === 'pinned' && !client.pinnedBudget.hasToken()) {
      return 'client_rate';
    }
    return this.laneBudgets[lane].hasToken() ? null : 'lane_rate';
  }

  // Spent only once every check passed: a refused render costs no budget.
  private admit(lane: RenderLane, client: ClientState): void {
    this.laneBudgets[lane].take();
    if (lane === 'pinned') {
      client.pinnedBudget.take();
    }
    this.inFlight[lane] += 1;
    client.inFlight += 1;
  }
}
