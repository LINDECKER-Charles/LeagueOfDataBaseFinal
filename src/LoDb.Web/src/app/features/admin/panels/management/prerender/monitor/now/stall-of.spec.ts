import type { PrerenderEnginePulse } from '../../../../../../../core/api/generated/models/prerender-engine-pulse';
import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';
import {
  prerenderPace,
  prerenderProgress,
} from '../../../../../testing/fixtures/prerender-progress';
import { stallOf } from './stall-of';

const BASE = prerenderProgress();
// The French unit wrote its last page at 08:42:03.
const LAST_PAGE = Date.parse('2026-10-21T08:42:03+00:00');
const MINUTE = 60_000;
const QUIET = LAST_PAGE + 4 * MINUTE;

function progress(
  pulse: Partial<PrerenderEnginePulse>,
  changes: Partial<PrerenderPassProgress> = {},
) {
  const observedAt = new Date(QUIET - 10_000).toISOString();
  return prerenderProgress({
    pulse: { ...BASE.pulse, observedAt, waiting: [], ...pulse },
    ...changes,
  });
}

describe('stallOf', () => {
  it('says nothing of a pass that is not running', () => {
    const paused = prerenderProgress({ pass: { ...BASE.pass, state: 'paused' } });

    expect(stallOf(paused, QUIET, LAST_PAGE)).toBeNull();
  });

  it('never stalls a paused pass whose unit stopped where it was', () => {
    const paused = prerenderProgress({
      pass: { ...BASE.pass, state: 'paused' },
      versions: [{ ...BASE.versions[0], cells: 'ccwcccz..............' }],
    });

    expect(stallOf(paused, LAST_PAGE + 60 * MINUTE, LAST_PAGE)).toBeNull();
  });

  it('waits for the worker in the first 90 seconds of a pass', () => {
    const createdAt = '2026-10-21T08:41:00+00:00';
    const units = { total: 42, pending: 42, running: 0, completed: 0, failed: 0 };
    const fresh = prerenderProgress({
      pass: { ...BASE.pass, createdAt, units },
      running: [],
      recent: [],
    });

    expect(stallOf(fresh, Date.parse(createdAt) + 60_000, Date.parse(createdAt))).toEqual({
      kind: 'starting',
    });
  });

  it('says nothing under 3 minutes without a page', () => {
    expect(stallOf(progress({}), LAST_PAGE + 2 * MINUTE, LAST_PAGE)).toBeNull();
  });

  it('names the first true cause, in order', () => {
    const since = '2026-10-21T08:43:00+00:00';
    const wait = BASE.pulse.waiting;
    const cause = (pulse: Partial<PrerenderEnginePulse>, changes = {}) => {
      const stall = stallOf(progress(pulse, changes), QUIET, LAST_PAGE);
      return stall?.kind === 'stalled' ? stall.cause : null;
    };

    expect(cause({ ssrUnreachableSince: since, isWorkerEnabled: false })).toBe('ssr');
    expect(cause({ isWorkerEnabled: false, waiting: wait })).toBe('worker_off');
    expect(cause({ waiting: wait }, { running: [] })).toBe('waiting');
    expect(cause({ waiting: wait })).toBe('unknown');
    expect(cause({ observedAt: null })).toBe('remote');
    expect(cause({ observedAt: new Date(QUIET - 3 * MINUTE).toISOString() })).toBe('remote');
    expect(cause({})).toBe('unknown');
  });

  it('names a held pass after the SSR, before every other cause', () => {
    const since = '2026-10-21T08:43:00+00:00';
    const pace = prerenderPace('held', { rendersPerMinute: 0, reason: 'host-cpu' });
    const cause = (pulse: Partial<PrerenderEnginePulse>) => {
      const stall = stallOf(progress({ pace, ...pulse }), QUIET, LAST_PAGE);
      return stall?.kind === 'stalled' ? stall.cause : null;
    };

    expect(cause({})).toBe('held');
    expect(cause({ isWorkerEnabled: false })).toBe('held');
    expect(cause({ observedAt: null })).toBe('held');
    expect(cause({ ssrUnreachableSince: since })).toBe('ssr');
    expect(cause({ pace: prerenderPace('slowed') })).toBe('unknown');
  });

  it('counts how long no page was written', () => {
    expect(stallOf(progress({}), QUIET, LAST_PAGE)).toEqual({
      kind: 'stalled',
      cause: 'unknown',
      idleSeconds: 240,
    });
  });

  it('stalls a unit whose pages all fail, though each failure rewrites it', () => {
    const failing = progress(
      {},
      { running: [{ ...BASE.running[0], updatedAt: '2026-10-21T08:46:00+00:00' }] },
    );

    expect(stallOf(failing, QUIET, LAST_PAGE)?.kind).toBe('stalled');
  });

  it('counts from the last resume: a resume writes no unit', () => {
    const resumedAt = new Date(QUIET - 30_000).toISOString();
    const resumed = progress({}, { pass: { ...BASE.pass, runningSince: resumedAt } });

    expect(stallOf(resumed, QUIET, LAST_PAGE - 60 * MINUTE)).toBeNull();
  });
});
