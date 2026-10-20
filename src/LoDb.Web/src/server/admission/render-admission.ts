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
import type { VisitorLane } from './visitor-lane';

const SERVICE_UNAVAILABLE = 503;
// Long enough for a crawler to back off, short enough for a visitor's reload.
const RETRY_AFTER_SECONDS = '30';

// Who a render counts against: the pass alone in its lane, a visitor with its own shares.
type Claim =
  | { readonly lane: 'prerender'; readonly client: null }
  | { readonly lane: VisitorLane; readonly client: ClientState };

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
 * ask little. Every client has its own share: renders at once, and renders per minute in
 * each lane, so that one client alone never takes what the others need. A slot is held until
 * the render settles, not until the client leaves, since a render whose client is gone still
 * costs its CPU. Over a 503, nginx serves the stale copy it may hold
 * (`proxy_cache_use_stale`).
 *
 * The pre-render pass has a lane of its own, chosen by its header whatever the URL, with
 * slots and a budget of its own and no client share: it is a single client, the API, which
 * a visitor's share would throttle. It starts a render only while the visitors keep their
 * reserve of free slots, and backs off on the 503's Retry-After like any client.
 */
export class RenderAdmission {
  private readonly inFlight: Record<RenderLane, number> = { pinned: 0, current: 0, prerender: 0 };
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
      prerender: new TokenBucket(limits.prerenderPerMinute, limits.prerenderBurst, now),
    };
    // A client's pinned share is small enough to be spent at once. Its current share is as
    // large as the lane's whole burst: it gets a burst of its own, below the lane's, or one
    // client spending it at once would empty the lane's burst for every other.
    const { pinnedPerClientPerMinute: pinned, currentPerClientPerMinute: current } = limits;
    const budgetsOf = () => ({
      pinned: new TokenBucket(pinned, pinned, now),
      current: new TokenBucket(current, limits.currentPerClientBurst, now),
    });
    this.clients = new ClientTable(limits.trackedClients, budgetsOf);
  }

  /** Renders running now in `lane`, for the metrics (`lodb_ssr_renders_in_flight`). */
  inFlightOf(lane: RenderLane): number {
    return this.inFlight[lane];
  }

  /** Runs `render` in the request's lane when admitted, else answers the 503. */
  async run(
    request: RenderRequest,
    response: Response,
    render: (lane: RenderLane) => Promise<void>,
  ): Promise<void> {
    const claim = this.claimOf(request);
    const refusal = this.refusalOf(claim);
    this.counts.record(claim.lane, refusal);
    if (refusal !== null) {
      refuse(response);
      return;
    }
    this.admit(claim);
    try {
      await render(claim.lane);
    } finally {
      this.release(claim);
    }
  }

  // The header beats the URL: a page of the pass names an older patch, which would put it in
  // the pinned lane and under a visitor's share. The pass is never a client of the table.
  private claimOf({ url, client, isPrerender }: RenderRequest): Claim {
    const lane = isPrerender ? 'prerender' : renderLaneOf(url);
    if (lane === 'prerender') {
      return { lane, client: null };
    }
    return { lane, client: this.clients.stateOf(client) };
  }

  private refusalOf(claim: Claim): RenderRefusal | null {
    const { pinned, current, prerender } = this.inFlight;
    if (pinned + current + prerender >= this.limits.maxInFlight) {
      return 'capacity';
    }
    return claim.lane === 'prerender'
      ? this.prerenderRefusal()
      : this.visitorRefusal(claim.lane, claim.client);
  }

  // Its own slots, then the visitors' reserve: the render it would start must leave them
  // `prerenderVisitorReserve` free slots. Its budget last, as for the other lanes.
  private prerenderRefusal(): RenderRefusal | null {
    const { maxInFlight, prerenderMaxInFlight, prerenderVisitorReserve } = this.limits;
    const { pinned, current, prerender } = this.inFlight;
    const isOverReserve = pinned + current + prerender + 1 > maxInFlight - prerenderVisitorReserve;
    if (prerender >= prerenderMaxInFlight || isOverReserve) {
      return 'lane_capacity';
    }
    return this.laneBudgets.prerender.hasToken() ? null : 'lane_rate';
  }

  // Slots first, budgets last, the client's before the lane's: a client over its own share
  // leaves the lane's budget to the others.
  private visitorRefusal(lane: VisitorLane, client: ClientState): RenderRefusal | null {
    const { limits } = this;
    if (client.inFlight >= limits.perClientMaxInFlight) {
      return 'client_capacity';
    }
    if (lane === 'pinned' && this.inFlight.pinned >= limits.pinnedMaxInFlight) {
      return 'lane_capacity';
    }
    if (!client.budgets[lane].hasToken()) {
      return 'client_rate';
    }
    return this.laneBudgets[lane].hasToken() ? null : 'lane_rate';
  }

  // Spent only once every check passed: a refused render costs no budget.
  private admit(claim: Claim): void {
    this.laneBudgets[claim.lane].take();
    this.inFlight[claim.lane] += 1;
    if (claim.lane !== 'prerender') {
      claim.client.budgets[claim.lane].take();
      claim.client.inFlight += 1;
    }
  }

  private release(claim: Claim): void {
    this.inFlight[claim.lane] -= 1;
    if (claim.client !== null) {
      claim.client.inFlight -= 1;
    }
  }
}
