import { HttpStatusCode } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import type { PrerenderEpochCoverage } from '../../../../../core/api/generated/models/prerender-epoch-coverage';
import type { PrerenderPassView } from '../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../core/api/generated/models/prerender-report';
import { Seo } from '../../../../../core/seo/seo';
import { configureAdminTestBed } from '../../../testing/admin-test-bed';
import type { AdminVisit } from '../../../testing/admin-visit';
import { press } from '../../../testing/dom/press';
import { prerenderProgress } from '../../../testing/fixtures/prerender-progress';
import { prerenderReport } from '../../../testing/fixtures/prerender-report';
import { sent } from '../../../testing/http/sent';
import { settle } from '../../../testing/http/settle';
import { panelRoute } from '../../../testing/panels/panel-route';
import { visitPanel } from '../../../testing/panels/visit-panel';
import { PrerenderPanel } from './prerender-panel';
import { openPrerender } from './prerender-panel-harness';

const PRERENDER = '/api/admin/prerender';
const PATCHES = `${PRERENDER}/patches`;
const PASS = prerenderProgress().pass;
const PROGRESS = { path: `${PRERENDER}/passes/12/progress`, body: prerenderProgress() };
const RUNNING = prerenderReport({ current: PASS, passes: [PASS, prerenderReport().passes[1]] });
const IDLE = prerenderReport({ current: null, passes: prerenderReport().passes.slice(1) });
const COVERAGE = prerenderReport().storage.epochs[0].coverage as PrerenderEpochCoverage;
const DISMISSED_KEY = 'lodb.admin.prerender.recap-dismissed';
const ENDED: PrerenderPassView = {
  ...PASS,
  state: 'cancelled',
  finishedAt: '2026-10-20T10:00:00Z',
};

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

function launchButton(visit: AdminVisit): HTMLButtonElement | null {
  return visit.page.querySelector<HTMLButtonElement>('lodb-launch-cta [data-launch]');
}

// A standby epoch 3 beside the served one: `coverage` says how far it is.
function withStandby(coverage: PrerenderEpochCoverage, changes: Partial<PrerenderReport> = {}) {
  const standby = { ...IDLE.storage.epochs[0], epoch: 3, state: 'standby', isWriting: false };
  return prerenderReport({
    ...IDLE,
    storage: { ...IDLE.storage, epochs: [{ ...standby, coverage }, ...IDLE.storage.epochs] },
    ...changes,
  });
}

describe('PrerenderPanel, its states', () => {
  afterEach(() => localStorage.removeItem(DISMISSED_KEY));

  it('says why its first read failed, and nothing else', async () => {
    configureAdminTestBed([panelRoute(PrerenderPanel, '/admin/prerender')]);
    const { page } = await visitPanel('/admin/prerender', [
      { path: PRERENDER, body: null, status: HttpStatusCode.BadGateway },
    ]);

    expect(page.querySelector('lodb-panel-state lodb-admin-band')).not.toBeNull();
    expect(page.querySelector('lodb-tabs')).toBeNull();
    expect(launchButton({ page } as AdminVisit)?.disabled).toBe(true);
    expect(texts(page, '[data-launch-lock]')).toEqual([
      'admin.prerender.overview.cta.locked_loading',
    ]);
  });

  it('follows a running pass: its monitor, the launch held and a way to it', async () => {
    const visit = await openPrerender({ report: RUNNING, calls: [PROGRESS] });

    expect(visit.page.querySelector('lodb-live-monitor')).not.toBeNull();
    expect(visit.page.querySelector('lodb-idle-card, lodb-pass-recap')).toBeNull();
    expect(launchButton(visit)?.disabled).toBe(true);
    expect(launchButton(visit)?.getAttribute('aria-describedby')).toBe('prerender-launch-lock');
    expect(texts(visit.page, '[data-launch-lock]')[0]).toContain(
      'admin.prerender.overview.cta.locked_active',
    );
    expect(texts(visit.page, '[data-launch-lock] button')).toEqual([
      'admin.prerender.overview.cta.follow',
    ]);
    expect(visit.page.querySelectorAll('lodb-kpi')).toHaveLength(4);
    expect(visit.page.querySelectorAll('lodb-tab')).toHaveLength(3);
  });

  it('keeps the report on screen, without a band, when a refresh fails', async () => {
    const visit = await openPrerender({ report: IDLE });

    press(visit, 'admin.prerender.overview.cta.refresh');
    (await sent(visit, PRERENDER)).flush(null, { status: 502, statusText: 'Bad Gateway' });
    (await sent(visit, PATCHES)).flush(null, { status: 502, statusText: 'Bad Gateway' });
    await settle(visit);

    expect(visit.page.querySelector('lodb-panel-state lodb-admin-band')).toBeNull();
    expect(visit.page.querySelector('lodb-idle-card')).not.toBeNull();
    expect(visit.page.querySelector('[data-coverage-error]')).not.toBeNull();
  });

  it('asks to sign in again once the session expired, the report kept', async () => {
    const visit = await openPrerender({ report: IDLE });

    press(visit, 'admin.prerender.overview.cta.refresh');
    (await sent(visit, PRERENDER)).flush(null, { status: 401, statusText: 'Unauthorized' });
    (await sent(visit, PATCHES)).flush(null, { status: 401, statusText: 'Unauthorized' });
    await settle(visit);

    expect(texts(visit.page, 'lodb-panel-state a')).toEqual(['admin.state.sign_in']);
    expect(visit.page.querySelector('lodb-idle-card')).not.toBeNull();
  });

  it('keeps the launch shut while the host is not configured, and says what to set', async () => {
    const visit = await openPrerender({ report: { ...IDLE, isConfigured: false } });

    expect(texts(visit.page, '[data-dormant]')).toEqual(['admin.prerender.overview.bands.dormant']);
    expect(launchButton(visit)?.disabled).toBe(true);
    expect(texts(visit.page, '[data-launch-lock]')).toEqual([
      'admin.prerender.overview.cta.locked_dormant',
    ]);
    expect(texts(visit.page, 'lodb-idle-card button')).toEqual([
      'admin.prerender.overview.idle.detail',
    ]);
    expect(visit.page.querySelector('lodb-tabs')).not.toBeNull();
  });

  it('says what to do without a pass: the last one, the gaps, a launch', async () => {
    const visit = await openPrerender({ report: IDLE });

    expect(launchButton(visit)?.disabled).toBe(false);
    expect(visit.page.querySelector('[data-launch-lock]')).toBeNull();
    expect(visit.page.querySelector('[data-last-pass]')).not.toBeNull();
    expect(texts(visit.page, '[data-gaps] button')).toEqual([
      'admin.prerender.overview.idle.gaps_cta',
    ]);
  });

  it('recaps a pass ended within a day, until this visitor dismisses it', async () => {
    const ended = prerenderReport({ ...IDLE, passes: [ENDED] });
    const visit = await openPrerender({ report: ended });
    expect(visit.page.querySelector('lodb-pass-recap')).not.toBeNull();

    press(visit, 'admin.prerender.live.recap.dismiss');
    await settle(visit);

    expect(visit.page.querySelector('lodb-pass-recap')).toBeNull();
    expect(visit.page.querySelector('lodb-idle-card')).not.toBeNull();
    expect(localStorage.getItem(DISMISSED_KEY)).toBe('12');
  });

  it('remembers a recap dismissed in this browser', async () => {
    localStorage.setItem(DISMISSED_KEY, '12');
    const visit = await openPrerender({ report: prerenderReport({ ...IDLE, passes: [ENDED] }) });

    expect(visit.page.querySelector('lodb-pass-recap')).toBeNull();
    expect(visit.page.querySelector('lodb-idle-card')).not.toBeNull();
  });

  it('says an epoch is ready, with what the SSR keeps serving', async () => {
    const whole = { ...COVERAGE, gapUnits: 0, completeUnits: 8_430 };
    const visit = await openPrerender({ report: withStandby(whole, { readyEpoch: 3 }) });

    expect(texts(visit.page, '[data-ready-epoch] [data-variable]')).toEqual([
      'LODB_ARCHIVE_SERVE_EPOCH=3',
    ]);
    expect(texts(visit.page, '[data-ready-ssr]')).toEqual([
      'admin.prerender.overview.bands.ready_failed_units',
      'admin.prerender.overview.bands.ready_failed_pages',
    ]);
    expect(visit.page.querySelector('[data-nearly-epoch]')).toBeNull();
  });

  it('offers to complete a standby epoch nearly whole', async () => {
    const nearly = { ...COVERAGE, gapUnits: 300, completeUnits: 8_142 };
    const visit = await openPrerender({ report: withStandby(nearly) });

    expect(texts(visit.page, '[data-nearly-epoch] button')).toEqual([
      'admin.prerender.overview.bands.nearly_cta',
    ]);
    expect(visit.page.querySelector('[data-ready-epoch]')).toBeNull();
  });

  it('titles the document with the share of the running pass', async () => {
    const apply = vi.fn(() => Promise.resolve());
    await openPrerender({
      report: RUNNING,
      calls: [PROGRESS],
      providers: [{ provide: Seo, useValue: { apply } }],
    });
    TestBed.tick();

    expect(apply).toHaveBeenLastCalledWith(
      expect.objectContaining({ title: 'admin.prerender.overview.doc_title.running' }),
    );
  });

  it('reads in French, the states by their names', async () => {
    const visit = await openPrerender({ report: RUNNING, calls: [PROGRESS], isFrench: true });

    expect(texts(visit.page, 'lodb-launch-cta [data-launch]')).toEqual(['Lancer une passe']);
    expect(texts(visit.page, '[data-launch-lock]')[0]).toContain(
      'Une passe est en cours (n°12) : une seule à la fois.',
    );
    expect(texts(visit.page, 'lodb-live-monitor [data-pass-state]')).toEqual(['en cours']);
    expect(texts(visit.page, '[data-kpi="cost"]')[0]).toContain(
      "+ < 0,01 € pour les epochs retirées, jusqu'à leur suppression",
    );
  });
});
