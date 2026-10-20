import type { Response } from 'express';
import { RenderAdmission } from './render-admission';
import { RenderCounts } from './render-counts';
import type { RenderLane } from './render-lane';
import type { RenderLimits } from './render-limits';
import type { RenderRequest } from './render-request';
import { SHED_MARKER } from './shed-marker';

const PINNED = '/fr/15.13.1/items/3031-infinity-edge';
const CURRENT = '/fr/items/3031-infinity-edge';
const VISITOR = '203.0.113.7';
const OTHER = '192.0.2.1';
const THIRD = '2001:db8::/64';
// The API's address on the stack network, the pass's only client.
const API = '172.18.0.5';
const LIMITS: RenderLimits = {
  maxInFlight: 4,
  pinnedMaxInFlight: 2,
  pinnedPerMinute: 60,
  pinnedBurst: 3,
  currentPerMinute: 60,
  currentBurst: 6,
  perClientMaxInFlight: 4,
  pinnedPerClientPerMinute: 60,
  currentPerClientPerMinute: 60,
  currentPerClientBurst: 6,
  trackedClients: 100,
  prerenderPerMinute: 60,
  prerenderBurst: 3,
  prerenderMaxInFlight: 2,
  prerenderVisitorReserve: 1,
};

interface Answer {
  status?: number;
  headers: Record<string, string>;
  locals: Record<string, unknown>;
  body?: string;
}

function responseOf(answer: Answer): Response {
  const response = {
    locals: answer.locals,
    set(fields: Record<string, string>) {
      Object.assign(answer.headers, fields);
      return response;
    },
    status(code: number) {
      answer.status = code;
      return response;
    },
    type: () => response,
    send(body: string) {
      answer.body = body;
      return response;
    },
  };
  return response as unknown as Response;
}

/** Starts a render that holds its slot until `finish` is called. */
function start(admission: RenderAdmission, request: RenderRequest) {
  const answer: Answer = { headers: {}, locals: {} };
  let finish = (): void => undefined;
  let lane: RenderLane | null = null;
  const done = admission.run(request, responseOf(answer), (admitted) => {
    lane = admitted;
    return new Promise<void>((resolve) => (finish = resolve));
  });
  return {
    answer,
    done,
    laneOf: () => lane,
    isStarted: () => lane !== null,
    finish: () => finish(),
  };
}

function hold(admission: RenderAdmission, url: string, client = VISITOR) {
  return start(admission, { url, client, isPrerender: false });
}

// A render of the pre-render pass: a page of an older patch, asked with the pass's header.
function holdPrerender(admission: RenderAdmission, url = PINNED) {
  return start(admission, { url, client: API, isPrerender: true });
}

async function settle(renders: ReturnType<typeof hold>[]): Promise<void> {
  renders.forEach((render) => render.finish());
  await Promise.all(renders.map((render) => render.done));
}

// One request per address, as from as many visitors.
function holdFromMany(admission: RenderAdmission, url: string, count: number) {
  return Array.from({ length: count }, (_, index) => hold(admission, url, `198.51.100.${index}`));
}

describe('RenderAdmission', () => {
  let now = 0;
  let counts: RenderCounts;
  const admissionOf = (limits: Partial<RenderLimits> = {}) =>
    new RenderAdmission({ ...LIMITS, ...limits }, counts, () => now);

  beforeEach(() => {
    now = 0;
    counts = new RenderCounts();
  });

  it('runs a render it has room for', async () => {
    const admission = admissionOf();
    const render = vi.fn(async () => undefined);
    const response = responseOf({ headers: {}, locals: {} });

    await admission.run({ url: CURRENT, client: VISITOR, isPrerender: false }, response, render);

    expect(render).toHaveBeenCalledOnce();
  });

  it('answers a 503 at once when every slot is taken, marked for the request log', () => {
    const admission = admissionOf();
    holdFromMany(admission, CURRENT, LIMITS.maxInFlight);

    const refused = hold(admission, CURRENT, OTHER);

    expect(refused.isStarted()).toBe(false);
    expect(refused.answer).toEqual({
      status: 503,
      headers: { 'Cache-Control': 'no-store', 'Retry-After': '30' },
      locals: { [SHED_MARKER]: true },
      body: 'Service Unavailable',
    });
  });

  it('refuses the third render a client runs at once, not another client', async () => {
    const admission = admissionOf({ perClientMaxInFlight: 2 });
    const held = [hold(admission, CURRENT), hold(admission, CURRENT)];

    expect(held.every((render) => render.isStarted())).toBe(true);
    expect(hold(admission, PINNED).answer.status).toBe(503);
    expect(hold(admission, CURRENT, OTHER).isStarted()).toBe(true);
    await settle(held);
    expect(hold(admission, CURRENT).isStarted()).toBe(true);
  });

  it('answers a 503 once the budget of the current pages is spent', async () => {
    const admission = admissionOf({ currentBurst: 2 });
    await settle(holdFromMany(admission, CURRENT, 2));

    expect(hold(admission, CURRENT, OTHER).answer.status).toBe(503);
    expect(hold(admission, PINNED, OTHER).isStarted()).toBe(true);
    now += 1_000;
    expect(hold(admission, CURRENT, OTHER).isStarted()).toBe(true);
  });

  it('keeps slots for the current pages when the pinned lane is full', () => {
    const admission = admissionOf();
    holdFromMany(admission, PINNED, 2);

    expect(hold(admission, PINNED, OTHER).answer.status).toBe(503);
    expect(hold(admission, CURRENT, OTHER).isStarted()).toBe(true);
  });

  it('refuses pinned renders past their budget, then admits them at the steady rate', async () => {
    const admission = admissionOf({ pinnedMaxInFlight: 3 });
    await settle(holdFromMany(admission, PINNED, LIMITS.pinnedBurst));

    expect(hold(admission, PINNED, OTHER).answer.status).toBe(503);
    expect(hold(admission, CURRENT, OTHER).isStarted()).toBe(true);
    now += 1_000;
    expect(hold(admission, PINNED, VISITOR).isStarted()).toBe(true);
  });

  it('refuses a client its pinned renders past its own budget, not the others', async () => {
    const admission = admissionOf({ pinnedPerClientPerMinute: 2, pinnedBurst: 10 });
    await settle([hold(admission, PINNED), hold(admission, PINNED)]);

    expect(hold(admission, PINNED).answer.status).toBe(503);
    expect(hold(admission, PINNED, OTHER).isStarted()).toBe(true);
    now += 30_000;
    expect(hold(admission, PINNED).isStarted()).toBe(true);
  });

  it('spends nothing from the lane on a client over its own budget', async () => {
    const admission = admissionOf({ pinnedPerClientPerMinute: 1, pinnedBurst: 2 });
    await settle([hold(admission, PINNED)]);

    expect(hold(admission, PINNED).answer.status).toBe(503);
    expect(hold(admission, PINNED, OTHER).isStarted()).toBe(true);
    expect(counts.entries()).toContainEqual({ lane: 'pinned', refusal: 'client_rate', count: 1 });
  });

  it('refuses a client its current renders past its own burst, not the others', async () => {
    const admission = admissionOf({ currentPerClientBurst: 3, currentBurst: 10 });
    await settle([hold(admission, CURRENT), hold(admission, CURRENT), hold(admission, CURRENT)]);

    expect(hold(admission, CURRENT).answer.status).toBe(503);
    expect(hold(admission, CURRENT, OTHER).isStarted()).toBe(true);
    expect(hold(admission, PINNED).isStarted()).toBe(true);
    now += 1_000;
    expect(hold(admission, CURRENT).isStarted()).toBe(true);
  });

  it("refills a client's share of the current pages at its own rate, not the lane's", async () => {
    const admission = admissionOf({ currentPerClientPerMinute: 6, currentPerClientBurst: 1 });
    await settle([hold(admission, CURRENT)]);

    now += 1_000;
    expect(hold(admission, CURRENT).answer.status).toBe(503);
    now += 9_000;
    expect(hold(admission, CURRENT).isStarted()).toBe(true);
  });

  it('spends nothing from the current lane on a client over its own share', async () => {
    const admission = admissionOf({ currentPerClientBurst: 1, currentBurst: 2 });
    const refusals = 5;
    await settle([hold(admission, CURRENT)]);
    const refused = Array.from({ length: refusals }, () => hold(admission, CURRENT));

    expect(refused.every((render) => render.answer.status === 503)).toBe(true);
    expect(hold(admission, CURRENT, OTHER).isStarted()).toBe(true);
    expect(hold(admission, CURRENT, THIRD).answer.status).toBe(503);
    expect(counts.entries()).toEqual([
      { lane: 'current', refusal: null, count: 2 },
      { lane: 'current', refusal: 'client_rate', count: refusals },
      { lane: 'current', refusal: 'lane_rate', count: 1 },
    ]);
  });

  it('spends no budget on a render refused for want of a slot', async () => {
    const admission = admissionOf({ pinnedBurst: 1 });
    const current = holdFromMany(admission, CURRENT, LIMITS.maxInFlight);
    hold(admission, PINNED, OTHER);

    await settle(current.slice(0, 1));

    expect(hold(admission, PINNED, OTHER).isStarted()).toBe(true);
  });

  it('frees the slot when the render settles, failed or not', async () => {
    const admission = admissionOf({ maxInFlight: 2, pinnedMaxInFlight: 1 });
    const failure = new Error('render failed');
    const response = responseOf({ headers: {}, locals: {} });

    await expect(
      admission.run({ url: CURRENT, client: VISITOR, isPrerender: false }, response, () =>
        Promise.reject(failure),
      ),
    ).rejects.toBe(failure);
    await settle([hold(admission, CURRENT)]);

    expect(hold(admission, CURRENT).isStarted()).toBe(true);
    expect(hold(admission, CURRENT).isStarted()).toBe(true);
  });

  it('hands the render its lane and reports the renders in flight per lane', async () => {
    const admission = admissionOf();
    const render = vi.fn(async () => undefined);
    const pinned = hold(admission, PINNED);
    hold(admission, CURRENT, OTHER);
    hold(admission, CURRENT, OTHER);

    expect(admission.inFlightOf('pinned')).toBe(1);
    expect(admission.inFlightOf('current')).toBe(2);
    await settle([pinned]);
    await admission.run(
      { url: PINNED, client: VISITOR, isPrerender: false },
      responseOf({ headers: {}, locals: {} }),
      render,
    );

    expect(render).toHaveBeenCalledWith('pinned');
    expect(admission.inFlightOf('pinned')).toBe(0);
  });

  it('counts every decision per lane and refusal', () => {
    const admission = admissionOf({ perClientMaxInFlight: 1 });
    hold(admission, CURRENT);
    hold(admission, CURRENT);
    hold(admission, PINNED, OTHER);

    expect(counts.entries()).toEqual([
      { lane: 'current', refusal: null, count: 1 },
      { lane: 'current', refusal: 'client_capacity', count: 1 },
      { lane: 'pinned', refusal: null, count: 1 },
    ]);
  });

  describe('pre-render lane', () => {
    it('runs a render of the pass in its own lane, whatever its URL', async () => {
      const admission = admissionOf();

      const pass = [holdPrerender(admission), holdPrerender(admission, CURRENT)];
      const visitor = hold(admission, PINNED);

      expect(pass.map((render) => render.laneOf())).toEqual(['prerender', 'prerender']);
      expect(visitor.laneOf()).toBe('pinned');
      expect(admission.inFlightOf('prerender')).toBe(2);
      expect(admission.inFlightOf('pinned')).toBe(1);
      await settle(pass);
      expect(admission.inFlightOf('prerender')).toBe(0);
    });

    it('takes no client share, nor from the client its address is', async () => {
      const admission = admissionOf({ perClientMaxInFlight: 1, pinnedPerClientPerMinute: 1 });

      const pass = [holdPrerender(admission), holdPrerender(admission)];

      expect(pass.every((render) => render.isStarted())).toBe(true);
      expect(hold(admission, PINNED, API).isStarted()).toBe(true);
      expect(counts.entries()).toEqual([
        { lane: 'prerender', refusal: null, count: 2 },
        { lane: 'pinned', refusal: null, count: 1 },
      ]);
    });

    it('refuses a render past its own slots, not a visitor', () => {
      const admission = admissionOf({ prerenderMaxInFlight: 1 });
      holdPrerender(admission);

      const refused = holdPrerender(admission);

      expect(refused.answer.status).toBe(503);
      expect(refused.answer.headers['Retry-After']).toBe('30');
      expect(hold(admission, PINNED).isStarted()).toBe(true);
      expect(counts.entries()).toContainEqual({
        lane: 'prerender',
        refusal: 'lane_capacity',
        count: 1,
      });
    });

    it('leaves the visitors their reserve of slots, which they may fill', () => {
      const admission = admissionOf({ prerenderMaxInFlight: 3, prerenderVisitorReserve: 2 });
      hold(admission, CURRENT);

      const admitted = holdPrerender(admission);
      const refused = holdPrerender(admission);

      expect(admitted.isStarted()).toBe(true);
      expect(refused.answer.status).toBe(503);
      expect(holdFromMany(admission, CURRENT, 2).every((render) => render.isStarted())).toBe(true);
      expect(counts.entries()).toContainEqual({
        lane: 'prerender',
        refusal: 'lane_capacity',
        count: 1,
      });
    });

    it('is refused for capacity once the visitors hold every slot', () => {
      const admission = admissionOf();
      holdFromMany(admission, CURRENT, LIMITS.maxInFlight);

      expect(holdPrerender(admission).answer.status).toBe(503);
      expect(counts.entries()).toContainEqual({ lane: 'prerender', refusal: 'capacity', count: 1 });
    });

    it('refuses renders past its budget, then admits them at its steady rate', async () => {
      const admission = admissionOf({ prerenderBurst: 2 });
      await settle([holdPrerender(admission), holdPrerender(admission)]);

      expect(holdPrerender(admission).answer.status).toBe(503);
      expect(hold(admission, PINNED).isStarted()).toBe(true);
      expect(counts.entries()).toContainEqual({
        lane: 'prerender',
        refusal: 'lane_rate',
        count: 1,
      });
      now += 1_000;
      expect(holdPrerender(admission).isStarted()).toBe(true);
    });
  });
});
