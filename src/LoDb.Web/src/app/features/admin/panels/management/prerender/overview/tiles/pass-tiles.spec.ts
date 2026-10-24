import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { historyTileOf } from './history-tile-of';
import { passTileOf } from './pass-tile-of';

// Texts read as `key {params}`, so a spec sees which key is chosen and what it is given.
const TEXTS: AdminText = {
  text: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
  term: (_group, name) => name,
};
const PASS = 'prerender.overview.views.pass';
const HISTORY = 'prerender.overview.views.history';
// The fixture's pass n°3 runs on epoch 2; n°2 was cancelled on 15/10/2026 at 09:00.
const REPORT = prerenderReport();
const [RUNNING, CANCELLED] = REPORT.passes;
const IDLE = prerenderReport({ current: null, passes: [CANCELLED] });

function active(changes: Partial<PrerenderPassView>) {
  const report = { ...REPORT, current: { ...RUNNING, ...changes } };
  return passTileOf({ report, recent: null, share: 0.5 }, TEXTS);
}

describe('passTileOf', () => {
  it('gives the share of a running pass, its number and its epoch, with a live dot', () => {
    expect(passTileOf({ report: REPORT, recent: null, share: 0.714 }, TEXTS)).toEqual({
      label: `${PASS}.label`,
      value: '71.4 %',
      sub: `${PASS}.sub_running {"id":3,"epoch":2}`,
      tone: 'live',
    });
  });

  it('marks a pass an administrator paused in gold, one that paused itself in red', () => {
    const byHand = active({ state: 'paused' });
    const itself = active({ state: 'paused', pauseReason: 'The store refused the key (403).' });

    expect([byHand.sub, byHand.tone]).toEqual([`${PASS}.sub_paused {"id":3,"epoch":2}`, 'warn']);
    expect([itself.sub, itself.tone]).toEqual([
      `${PASS}.sub_paused_itself {"id":3,"epoch":2}`,
      'bad',
    ]);
  });

  it('says how a pass that ended within a day ended, and when', () => {
    const completed = { ...CANCELLED, state: 'completed' };

    expect(passTileOf({ report: IDLE, recent: CANCELLED, share: 0 }, TEXTS)).toMatchObject({
      value: `${PASS}.value_cancelled`,
      sub: `${PASS}.sub_ended {"id":2,"date":"15/10/2026 09:00"}`,
      tone: 'muted',
    });
    expect(passTileOf({ report: IDLE, recent: completed, share: 0 }, TEXTS)).toMatchObject({
      value: `${PASS}.value_completed`,
      tone: 'good',
    });
  });

  it('says without a pass whether the promotions open one, and that none starts unconfigured', () => {
    expect(passTileOf({ report: IDLE, recent: null, share: 0 }, TEXTS)).toMatchObject({
      value: `${PASS}.value_idle`,
      sub: `${PASS}.sub_idle {"promotions":"on"}`,
      tone: null,
    });
    const dormant = { ...IDLE, isConfigured: false };
    expect(passTileOf({ report: dormant, recent: null, share: 0 }, TEXTS)).toMatchObject({
      sub: `${PASS}.sub_dormant`,
      tone: 'bad',
    });
  });
});

describe('historyTileOf', () => {
  it('counts the passes listed, and says how the newest stands', () => {
    expect(historyTileOf(REPORT, TEXTS)).toEqual({
      label: `${HISTORY}.label`,
      value: `${HISTORY}.value {"count":2}`,
      sub: `${HISTORY}.sub {"id":3,"state":"running","date":"18/10/2026"}`,
      tone: null,
    });
  });

  it('says when no pass was launched yet', () => {
    const empty = prerenderReport({ current: null, passes: [] });

    expect(historyTileOf(empty, TEXTS)).toMatchObject({
      value: `${HISTORY}.value {"count":0}`,
      sub: `${HISTORY}.sub_none`,
    });
  });
});
