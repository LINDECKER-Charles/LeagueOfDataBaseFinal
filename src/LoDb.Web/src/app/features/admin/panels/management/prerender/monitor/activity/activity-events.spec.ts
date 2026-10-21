import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { type ActivityBefore, activityEvents } from './activity-events';

const FIRST = prerenderProgress();
const NEXT_AT = '2026-10-21T08:42:10+00:00';
const JOINED = { ...FIRST.versions[0], version: '16.20.1', rank: 1, cells: '.'.repeat(21) };

function before(progress: PrerenderPassProgress = FIRST): ActivityBefore {
  return { progress };
}

function next(pass: Partial<PrerenderPassView>, changes: Partial<PrerenderPassProgress> = {}) {
  return prerenderProgress({
    generatedAt: NEXT_AT,
    pass: { ...FIRST.pass, ...pass },
    ...changes,
  });
}

function kinds(previous: ActivityBefore | undefined, progress: PrerenderPassProgress): string[] {
  const seen = new Set(previous ? ['16.18.1|es|2026-10-21T08:38:00+00:00'] : []);
  return activityEvents(previous, progress, seen).events.map((event) => event.kind);
}

describe('activityEvents', () => {
  it('takes the units that ended at the first answer, and nothing of the pass', () => {
    const { events, seen } = activityEvents(undefined, FIRST, new Set());

    expect(events).toEqual([
      {
        key: 'unit:16.18.1|es|2026-10-21T08:38:00+00:00',
        kind: 'done',
        at: '2026-10-21T08:38:00+00:00',
        version: '16.18.1',
        locale: 'es',
        pages: 1_140,
        failed: 0,
        seconds: 365,
        reason: null,
      },
    ]);
    expect([...seen]).toEqual(['16.18.1|es|2026-10-21T08:38:00+00:00']);
  });

  it('takes a unit once', () => {
    expect(kinds(before(), next({}))).toEqual([]);
  });

  it('says a unit failed, or lost pages, with the reason', () => {
    const lost = { ...FIRST.failures[0], updatedAt: NEXT_AT, endedAt: NEXT_AT };
    const failed = { ...lost, locale: 'th', state: 'failed', failure: 'Unknown version.' };
    const { events } = activityEvents(before(), next({}, { recent: [failed, lost] }), new Set());

    expect(events.map((event) => [event.kind, event.reason])).toEqual([
      ['failed', 'Unknown version.'],
      ['done_failures', 'The SSR answered 500, not a page to store.'],
    ]);
  });

  it('says a version joined the pass, with its units', () => {
    const progress = next({}, { versions: [JOINED] });
    const { events } = activityEvents(before(), progress, new Set());

    expect(events.find((event) => event.kind === 'joined')).toMatchObject({
      version: '16.20.1',
      units: 21,
    });
  });

  it('says the total grew by more than 1 %, and why when a version joined', () => {
    const grown = { pagesLeft: (FIRST.pass.pagesLeft ?? 0) + 24_000 };

    expect(kinds(before(), next(grown))).toEqual(['total_up']);
    expect(kinds(before(), next(grown, { versions: [JOINED] }))).toEqual([
      'joined',
      'total_up_joined',
    ]);
    const { events } = activityEvents(before(), next(grown), new Set(['x']));
    expect(events.find((event) => event.kind === 'total_up')?.pages).toBe(24_000);
  });

  it('says nothing of a total that grew by 1 % or less', () => {
    expect(kinds(before(), next({ pagesLeft: (FIRST.pass.pagesLeft ?? 0) + 21_000 }))).toEqual([]);
  });

  it('says each move of the pass', () => {
    const paused = next({ state: 'paused' });

    expect(kinds(before(), paused)).toEqual(['paused']);
    expect(kinds(before(paused), next({}))).toEqual(['resumed']);
    expect(kinds(before(), next({ state: 'completed' }))).toEqual(['completed']);
    expect(kinds(before(), next({ state: 'cancelled' }))).toEqual(['cancelled']);
  });

  it('says the pass paused itself, with its reason', () => {
    const reason = 'The store refused the writer key (403).';
    const { events } = activityEvents(
      before(),
      next({ state: 'paused', pauseReason: reason }),
      new Set(['16.18.1|es|2026-10-21T08:38:00+00:00']),
    );

    expect(events).toEqual([
      { key: `pass:paused_itself:${NEXT_AT}`, kind: 'paused_itself', at: NEXT_AT, reason },
    ]);
  });

  it('starts over for another pass', () => {
    expect(kinds(before(), next({ id: 13, state: 'paused' }))).toEqual(['done']);
  });
});
