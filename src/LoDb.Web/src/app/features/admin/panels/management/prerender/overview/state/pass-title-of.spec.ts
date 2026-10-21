import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { passTitleOf } from './pass-title-of';

const TITLE = 'prerender.overview.doc_title';
const RUNNING = prerenderReport().current as PrerenderPassView;
// 91,230 pages processed of 91,230 + 75,658: 54.7 %.
const SHARE = 91_230 / (91_230 + 75_658);
const ENDED: PrerenderPassView = {
  ...RUNNING,
  id: 12,
  state: 'completed',
  finishedAt: '2026-10-20T08:00:00Z',
};

function ended(changes: Partial<PrerenderPassView>): PrerenderReport {
  return prerenderReport({ current: null, passes: [{ ...ENDED, ...changes }] });
}

describe('passTitleOf', () => {
  it('gives the share of a running pass, to the whole percent', () => {
    expect(passTitleOf(prerenderReport(), null, null)).toEqual({
      key: `${TITLE}.running`,
      params: { share: '55 %' },
      held: { passId: 3, share: SHARE },
    });
  });

  it('marks a paused pass', () => {
    const paused = prerenderReport({ current: { ...RUNNING, state: 'paused' } });
    expect(passTitleOf(paused, null, null).key).toBe(`${TITLE}.paused`);
  });

  it('never goes back for the same pass, and starts again for another', () => {
    expect(passTitleOf(prerenderReport(), null, { passId: 3, share: 0.71 }).params).toEqual({
      share: '71 %',
    });
    expect(passTitleOf(prerenderReport(), null, { passId: 2, share: 0.71 }).params).toEqual({
      share: '55 %',
    });
  });

  it('names a pass completed or cancelled within a day', () => {
    expect(passTitleOf(ended({}), null, null)).toEqual({
      key: `${TITLE}.completed`,
      params: { id: 12 },
      held: null,
    });
    expect(passTitleOf(ended({ state: 'cancelled' }), null, null).key).toBe(`${TITLE}.cancelled`);
  });

  it('forgets a pass dismissed, or ended more than a day ago', () => {
    expect(passTitleOf(ended({}), 12, null).key).toBe(`${TITLE}.idle`);
    expect(passTitleOf(ended({ finishedAt: '2026-10-19T11:59:00Z' }), null, null).key).toBe(
      `${TITLE}.idle`,
    );
  });

  it('is the name of the page alone without a pass, or before the first report', () => {
    expect(passTitleOf(prerenderReport({ current: null, passes: [] }), null, null).key).toBe(
      `${TITLE}.idle`,
    );
    expect(passTitleOf(undefined, null, null)).toEqual({
      key: `${TITLE}.idle`,
      params: {},
      held: null,
    });
  });
});
