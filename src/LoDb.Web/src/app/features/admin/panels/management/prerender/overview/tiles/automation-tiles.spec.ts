import type { AdminText } from '../../../../../shared/inject-admin-text';
import { prerenderDemand } from '../../../../../testing/fixtures/prerender-demand';
import { prerenderSpeed } from '../../../../../testing/fixtures/prerender-speed';
import { prerenderRevision } from '../../../../../testing/fixtures/revision/prerender-revision';
import { queuesTileOf } from './queues-tile-of';
import { speedTileOf } from './speed-tile-of';

// Texts read as `key {params}`, so a spec sees which key is chosen and what it is given.
const TEXTS: AdminText = {
  text: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
  term: (_group, name) => name,
};
const QUEUES = 'prerender.overview.views.queues';
const SPEED = 'prerender.overview.views.speed';
// 412 refused pages pending, 3 of them rendering; 1 806 pages of closed states pending.
const DEMAND = prerenderDemand();
const REVISION = prerenderRevision();

describe('queuesTileOf', () => {
  it('sums the pages both queues hold, each one named, live while one renders', () => {
    expect(queuesTileOf({ demand: DEMAND, revision: REVISION }, TEXTS)).toEqual({
      label: `${QUEUES}.label`,
      value: '2 218',
      sub: `${QUEUES}.sub {"demand":"412","revision":"1 806"}`,
      tone: 'live',
    });
  });

  it('reads dashes before either report, and nothing worth a dot while all waits', () => {
    expect(queuesTileOf({ demand: undefined, revision: undefined }, TEXTS)).toMatchObject({
      value: '—',
      sub: `${QUEUES}.sub {"demand":"—","revision":"—"}`,
      tone: null,
    });
    const quiet = { ...DEMAND, rendering: 0 };
    expect(queuesTileOf({ demand: quiet, revision: REVISION }, TEXTS).tone).toBeNull();
  });

  it('warns of a queue that cannot read the refused pages, and greys one asleep', () => {
    const blind = { ...DEMAND, isCapturing: false };
    const off = { ...DEMAND, isEnabled: false };
    const dormant = { ...REVISION, isActive: false };

    expect(queuesTileOf({ demand: blind, revision: REVISION }, TEXTS).tone).toBe('warn');
    expect(queuesTileOf({ demand: off, revision: REVISION }, TEXTS).tone).toBe('muted');
    expect(queuesTileOf({ demand: DEMAND, revision: dormant }, TEXTS).tone).toBe('muted');
  });
});

describe('speedTileOf', () => {
  it('names the adaptive mode and its range while no pass drains', () => {
    expect(speedTileOf(prerenderSpeed(), TEXTS)).toEqual({
      label: `${SPEED}.label`,
      value: `${SPEED}.value_adaptive`,
      sub: `${SPEED}.sub_adaptive {"min":30,"max":240}`,
      tone: null,
    });
  });

  it('gives the pace in force while a pass drains, in gold while the brake holds it', () => {
    const pace = {
      mode: 'adaptive',
      state: 'held',
      rendersPerMinute: 0,
      minRendersPerMinute: 30,
      maxRendersPerMinute: 240,
      since: '2026-10-21T21:25:00Z',
    };

    expect(speedTileOf(prerenderSpeed({ pace }), TEXTS)).toMatchObject({
      sub: `${SPEED}.sub_now {"pace":0}`,
      tone: 'warn',
    });
    const climbing = { ...pace, state: 'climbing', rendersPerMinute: 83.6 };
    expect(speedTileOf(prerenderSpeed({ pace: climbing }), TEXTS)).toMatchObject({
      sub: `${SPEED}.sub_now {"pace":84}`,
      tone: null,
    });
  });

  it('gives the manual target, in gold past what the renderers sustain', () => {
    const setting = { mode: 'manual', rendersPerMinute: 1_500, updatedAt: null };

    expect(speedTileOf(prerenderSpeed({ setting }), TEXTS)).toEqual({
      label: `${SPEED}.label`,
      value: `${SPEED}.value_manual`,
      sub: `${SPEED}.sub_manual {"pace":1500}`,
      tone: 'warn',
    });
  });

  it('says the environment fixed the pace, and reads a dash before the first report', () => {
    expect(speedTileOf(prerenderSpeed({ isFixed: true }), TEXTS)).toMatchObject({
      value: `${SPEED}.value_fixed`,
      sub: `${SPEED}.sub_fixed`,
      tone: 'warn',
    });
    expect(speedTileOf(undefined, TEXTS)).toMatchObject({ value: '—', sub: null, tone: null });
  });
});
