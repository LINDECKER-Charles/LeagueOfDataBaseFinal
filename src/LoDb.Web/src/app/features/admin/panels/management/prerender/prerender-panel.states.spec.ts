import { HttpStatusCode } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import type { PrerenderEpochCoverage } from '../../../../../core/api/generated/models/prerender-epoch-coverage';
import type { PrerenderPassView } from '../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../core/api/generated/models/prerender-report';
import { Seo } from '../../../../../core/seo/seo';
import { AdminNotices } from '../../../layout/admin-notices';
import { PRERENDER_CADENCE } from './common/scope/prerender-cadence';
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
import { BESIDE_THE_REPORT, openPrerender } from './prerender-panel-harness';

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

// The tiles of the views as read: each one's name, figure and line, one after the other.
function tiles(visit: AdminVisit): string[] {
  return [...visit.page.querySelectorAll('[role="tab"]')].map((tab) =>
    ['[data-tile-name]', '[data-tile-value]', '[data-tile-sub]']
      .flatMap((part) => texts(tab, part))
      .join(' '),
  );
}

// Whether the panel of a view shows: every one is rendered, the others hidden.
function isShown(visit: AdminVisit, view: string): boolean {
  return visit.page.querySelector<HTMLElement>(`#prerender-${view}-panel`)?.hidden === false;
}

async function choose(visit: AdminVisit, view: string): Promise<void> {
  visit.page.querySelector<HTMLButtonElement>(`[data-view="prerender-${view}"]`)?.click();
  await settle(visit);
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
      ...BESIDE_THE_REPORT,
    ]);

    expect(page.querySelector('lodb-panel-state lodb-admin-band')).not.toBeNull();
    expect(page.querySelector('lodb-status-tabs')).toBeNull();
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
    expect(visit.page.querySelectorAll('[role="tab"]')).toHaveLength(6);
    expect(isShown(visit, 'pass')).toBe(true);
    expect(isShown(visit, 'coverage')).toBe(false);
  });

  it('opens the view its URL names, and writes the one chosen in the URL', async () => {
    const visit = await openPrerender({ report: IDLE, url: '/admin/prerender?view=history' });
    const router = TestBed.inject(Router);
    expect(isShown(visit, 'history')).toBe(true);
    expect(isShown(visit, 'pass')).toBe(false);

    await choose(visit, 'coverage');
    expect(isShown(visit, 'coverage')).toBe(true);
    expect(router.url).toBe('/admin/prerender?view=coverage');

    await choose(visit, 'pass');
    expect(router.url).toBe('/admin/prerender');
  });

  it('takes the operator from another view to the monitor of the active pass', async () => {
    const visit = await openPrerender({
      report: RUNNING,
      calls: [PROGRESS],
      url: '/admin/prerender?view=storage',
    });
    expect(isShown(visit, 'pass')).toBe(false);

    press(visit, 'admin.prerender.overview.cta.follow');
    await settle(visit);

    expect(isShown(visit, 'pass')).toBe(true);
    expect(document.activeElement?.id).toBe('prerender-monitor');
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
    expect(visit.page.querySelector('lodb-status-tabs')).not.toBeNull();
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

  it('takes away the band of a launch once its pass is seen ending', async () => {
    const launched = new AdminNotices();
    launched.post({ tone: 'notice', text: "Passe n°12 lancée sur l'epoch 2 : 4 unités." });
    // The monitor reads its progress again at once: the second answer ends the pass.
    const cadence = {
      progressRunningMs: 50,
      progressPausedMs: 30_000,
      reportActiveMs: 60_000,
      reportIdleMs: 120_000,
      reportHiddenMs: 300_000,
      versionMs: 10_000,
    };
    const visit = await openPrerender({
      report: RUNNING,
      calls: [PROGRESS],
      providers: [
        { provide: AdminNotices, useValue: launched },
        { provide: PRERENDER_CADENCE, useValue: cadence },
      ],
    });
    expect(launched.current()).not.toBeNull();

    const completed = prerenderProgress({ pass: { ...PASS, state: 'completed' } });
    (await sent(visit, PROGRESS.path)).flush(completed);
    for (let step = 0; launched.current() !== null && step < 100; step++) {
      await new Promise((resolve) => setTimeout(resolve, 10));
      visit.harness.detectChanges();
    }

    expect(launched.current()).toBeNull();
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
    expect(tiles(visit)).toEqual([
      'Passe 71.4 % n°12 · epoch 2 · en cours',
      'Couverture 98 % epoch 2 servie · 391 / 402 patchs complets',
      "Files automatiques 2 218 en attente : 412 refusées (503) · 1 806 d'états clos",
      'Vitesse Adaptative entre 30 et 240 pages/min',
      'Stockage et coûts 9.26 GB 0,07 € / mois · 91 230 pages',
      'Historique 2 passes dernière : n°12 (en cours), 18/10/2026',
    ]);
  });
});
