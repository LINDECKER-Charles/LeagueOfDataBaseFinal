import type { Response } from 'express';
import { RenderAdmission } from './render-admission';
import type { RenderLimits } from './render-limits';

const PINNED = '/fr/15.13.1/items/3031-infinity-edge';
const CURRENT = '/fr/items/3031-infinity-edge';
const LIMITS: RenderLimits = {
  maxInFlight: 4,
  pinnedMaxInFlight: 2,
  pinnedPerMinute: 60,
  pinnedBurst: 3,
};

interface Answer {
  status?: number;
  headers: Record<string, string>;
  body?: string;
}

function responseOf(answer: Answer): Response {
  const response = {
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
function hold(admission: RenderAdmission, url: string) {
  const answer: Answer = { headers: {} };
  let finish = (): void => undefined;
  let started = false;
  const done = admission.run(url, responseOf(answer), () => {
    started = true;
    return new Promise<void>((resolve) => (finish = resolve));
  });
  return { answer, done, isStarted: () => started, finish: () => finish() };
}

describe('RenderAdmission', () => {
  let now = 0;
  const admissionOf = (limits: RenderLimits = LIMITS) => new RenderAdmission(limits, () => now);

  beforeEach(() => (now = 0));

  it('runs a render it has room for', async () => {
    const admission = admissionOf();
    const render = vi.fn(async () => undefined);

    await admission.run(CURRENT, responseOf({ headers: {} }), render);

    expect(render).toHaveBeenCalledOnce();
  });

  it('answers a 503 at once when every slot is taken', () => {
    const admission = admissionOf();
    Array.from({ length: LIMITS.maxInFlight }, () => hold(admission, CURRENT));

    const refused = hold(admission, CURRENT);

    expect(refused.isStarted()).toBe(false);
    expect(refused.answer).toEqual({
      status: 503,
      headers: { 'Cache-Control': 'no-store', 'Retry-After': '30' },
      body: 'Service Unavailable',
    });
  });

  it('keeps slots for the current pages when the pinned lane is full', () => {
    const admission = admissionOf();
    hold(admission, PINNED);
    hold(admission, PINNED);

    expect(hold(admission, PINNED).answer.status).toBe(503);
    expect(hold(admission, CURRENT).isStarted()).toBe(true);
  });

  it('refuses pinned renders past their budget, then admits them at the steady rate', async () => {
    const admission = admissionOf({ ...LIMITS, pinnedMaxInFlight: 3 });
    const burst = Array.from({ length: LIMITS.pinnedBurst }, () => hold(admission, PINNED));
    burst.forEach((render) => render.finish());
    await Promise.all(burst.map((render) => render.done));

    expect(hold(admission, PINNED).answer.status).toBe(503);
    expect(hold(admission, CURRENT).isStarted()).toBe(true);
    now += 1_000;
    expect(hold(admission, PINNED).isStarted()).toBe(true);
  });

  it('spends no budget on a render refused for want of a slot', async () => {
    const admission = admissionOf({ ...LIMITS, pinnedBurst: 1 });
    const current = Array.from({ length: LIMITS.maxInFlight }, () => hold(admission, CURRENT));
    hold(admission, PINNED);

    current[0]?.finish();
    await current[0]?.done;

    expect(hold(admission, PINNED).isStarted()).toBe(true);
  });

  it('frees the slot when the render settles, failed or not', async () => {
    const admission = admissionOf({ ...LIMITS, maxInFlight: 2, pinnedMaxInFlight: 1 });
    const failure = new Error('render failed');

    await expect(
      admission.run(CURRENT, responseOf({ headers: {} }), () => Promise.reject(failure)),
    ).rejects.toBe(failure);
    const held = hold(admission, CURRENT);
    held.finish();
    await held.done;

    expect(hold(admission, CURRENT).isStarted()).toBe(true);
    expect(hold(admission, CURRENT).isStarted()).toBe(true);
  });
});
