import { HttpStatusCode } from '@angular/common/http';
import type { PrerenderPassView } from '../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../core/api/generated/models/prerender-report';
import { ADMIN_API } from '../../../testing/admin-api';
import { configureAdminTestBed } from '../../../testing/admin-test-bed';
import type { AdminVisit } from '../../../testing/admin-visit';
import { notice } from '../../../testing/dom/notice';
import { press } from '../../../testing/dom/press';
import { prerenderReport } from '../../../testing/fixtures/prerender-report';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { openPanel } from '../../../testing/panels/open-panel';
import { openPanelInFrench } from '../../../testing/panels/open-panel-in-french';
import { panelRoute } from '../../../testing/panels/panel-route';
import { visitPanel } from '../../../testing/panels/visit-panel';
import { PRERENDER_POLL } from './prerender-poll';
import { PrerenderPanel } from './prerender-panel';

const PRERENDER = '/api/admin/prerender';
const PASSES = `${PRERENDER}/passes`;
const URL = '/admin/prerender';
// Polled at once in a spec, then waited for a few rounds once the pass ended.
const SPEC_POLL_MS = 30;
const SPEC_WAIT_MS = 120;
const BAD_GATEWAY = { status: HttpStatusCode.BadGateway, statusText: 'Bad Gateway' };
const CONFLICT = { status: HttpStatusCode.Conflict, statusText: 'Conflict' };

const RUNNING = prerenderReport().current as PrerenderPassView;
const IDLE = prerenderReport({ current: null, passes: prerenderReport().passes.slice(1) });

function withPass(state: string): PrerenderReport {
  return prerenderReport({ current: { ...RUNNING, state } });
}

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

function open(report: PrerenderReport = prerenderReport()) {
  return openPanel(PrerenderPanel, URL, [{ path: PRERENDER, body: report }]);
}

function field(visit: AdminVisit, name: string, value: string): void {
  const found = visit.page.querySelector<HTMLInputElement>(`[name="${name}"]`);
  if (!found) {
    throw new Error(`No field is named "${name}".`);
  }
  found.value = value;
}

function launchButton(visit: AdminVisit): HTMLButtonElement | undefined {
  return [...visit.page.querySelectorAll<HTMLButtonElement>('lodb-launch-form button')].find(
    (button) => button.textContent?.trim() === 'admin.prerender.launch.submit',
  );
}

async function readAgain(visit: AdminVisit, report: PrerenderReport): Promise<void> {
  await reply(visit, await sent(visit, PRERENDER), report);
}

describe('PrerenderPanel', () => {
  it('shows the key figures, the running pass, the store and the latest passes', async () => {
    const { page } = await open();

    expect(texts(page, 'lodb-kpi')).toEqual([
      'admin.prerender.kpi.served 2 admin.prerender.kpi.served_sub',
      'admin.prerender.kpi.pass admin.prerender.states.running admin.prerender.kpi.pass_sub',
      'admin.prerender.kpi.stored 9.26 GB admin.prerender.kpi.stored_sub',
      'admin.prerender.kpi.cost 0,07 € admin.prerender.kpi.cost_sub_retired',
    ]);
    expect(page.querySelector('progress')?.value).toBeCloseTo(150 / 210);
    expect(texts(page, '[data-readout="units"] .readout-value')).toEqual(['150 / 210']);
    expect(texts(page, '[data-readout="pages"] .readout-value')).toEqual(['91 230']);
    expect(texts(page, '[data-readout="end"] .readout-value')).toEqual(['21/10/2026 02:00']);
    expect(texts(page, 'lodb-current-pass-card button')).toEqual([
      'admin.prerender.current.pause',
      'admin.prerender.current.cancel',
    ]);
    expect(texts(page, 'tr[data-epoch="2"] td')).toEqual([
      'admin.prerender.storage.epoch',
      'admin.prerender.states.served admin.prerender.states.writing',
      '91 230',
      '9.24 GB',
      '0,07 €',
    ]);
    expect(texts(page, 'tr[data-epoch="1"] td')[4]).toBe('< 0,01 €');
    expect(texts(page, 'tr[data-builds] td').slice(3)).toEqual(['24.00 MB', '< 0,01 €']);
    expect(texts(page, 'tr[data-total] td').slice(1)).toEqual(['91 230', '9.26 GB', '0,07 €']);
    expect(page.querySelector('[data-retired]')).not.toBeNull();
    expect(page.querySelector('[data-egress]')).not.toBeNull();
    expect(page.querySelector('[data-counters]')).not.toBeNull();
    expect(page.querySelectorAll('tr[data-version]')).toHaveLength(2);
    expect(texts(page, 'tr[data-pass="2"] td').slice(0, 3)).toEqual([
      '#2',
      '1',
      'admin.prerender.states.cancelled',
    ]);
  });

  it('pauses a running pass, then shows it paused', async () => {
    const visit = await open();

    press(visit, 'admin.prerender.current.pause');
    (await sent(visit, `${PASSES}/3/pause`, 'POST')).flush('');
    await readAgain(visit, withPass('paused'));

    expect(notice()).toBe('notice: admin.prerender.done.pause');
    expect(texts(visit.page, '[data-pass-state]')).toEqual(['admin.prerender.states.paused']);
    expect(texts(visit.page, 'lodb-current-pass-card button')).toEqual([
      'admin.prerender.current.resume',
      'admin.prerender.current.cancel',
    ]);
  });

  it('says the pass moved when a pause comes too late, then shows where it is', async () => {
    const visit = await open();

    press(visit, 'admin.prerender.current.pause');
    const problem = { status: HttpStatusCode.Conflict, code: 'prerender-pass-state' };
    (await sent(visit, `${PASSES}/3/pause`, 'POST')).flush(problem, CONFLICT);
    await readAgain(visit, withPass('paused'));

    expect(notice()).toBe('alert: admin.errors.prerender_state');
    expect(texts(visit.page, '[data-pass-state]')).toEqual(['admin.prerender.states.paused']);
  });

  it('resumes a paused pass', async () => {
    const visit = await open(withPass('paused'));

    press(visit, 'admin.prerender.current.resume');
    (await sent(visit, `${PASSES}/3/resume`, 'POST')).flush('');
    await readAgain(visit, withPass('running'));

    expect(notice()).toBe('notice: admin.prerender.done.resume');
    expect(texts(visit.page, '[data-pass-state]')).toEqual(['admin.prerender.states.running']);
  });

  it('cancels the pass once confirmed, then shows none active', async () => {
    const visit = await open();

    press(visit, 'admin.prerender.current.cancel');
    press(visit, 'admin.prerender.current.confirm_cancel');
    (await sent(visit, `${PASSES}/3/cancel`, 'POST')).flush('');
    await readAgain(visit, IDLE);

    expect(notice()).toBe('notice: admin.prerender.done.cancel');
    expect(visit.page.querySelector('lodb-current-pass-card')).toBeNull();
    expect(texts(visit.page, '[data-no-pass]')).toEqual(['admin.prerender.current.none']);
    expect(launchButton(visit)?.disabled).toBe(false);
  });

  it('launches a pass over its perimeter once confirmed', async () => {
    const visit = await open(IDLE);

    field(visit, 'epoch', '3');
    field(visit, 'oldest', '14.1.1');
    field(visit, 'pageType', 'items');
    field(visit, 'locale', 'fr');
    field(visit, 'priority', '16.18.1\n16.17.1, 16.18.1');
    press(visit, 'admin.prerender.launch.submit');
    press(visit, 'admin.prerender.launch.confirm');
    const start = await sent(visit, PASSES, 'POST');
    start.flush({ id: 4, epoch: 3, units: 21 });
    await readAgain(visit, prerenderReport({ current: { ...RUNNING, id: 4, epoch: 3 } }));

    expect(start.request.body).toEqual({
      epoch: 3,
      oldest: '14.1.1',
      newest: null,
      pageType: 'items',
      locale: 'fr',
      priority: ['16.18.1', '16.17.1'],
    });
    expect(notice()).toBe('notice: admin.prerender.done.start');
    expect(texts(visit.page, '[data-launch-lock]')).toEqual([
      'admin.prerender.launch.locked_active',
    ]);
  });

  it('says why the API refused a second pass, then shows the one active', async () => {
    const visit = await open(IDLE);

    press(visit, 'admin.prerender.launch.submit');
    press(visit, 'admin.prerender.launch.confirm');
    const problem = { status: HttpStatusCode.Conflict, code: 'prerender-pass-active' };
    (await sent(visit, PASSES, 'POST')).flush(problem, CONFLICT);
    await readAgain(visit, prerenderReport());

    expect(notice()).toBe('alert: admin.errors.prerender_active');
    expect(visit.page.querySelector('lodb-current-pass-card')).not.toBeNull();
    expect(launchButton(visit)?.disabled).toBe(true);
  });

  it('keeps the launch shut while the pre-rendering is not configured', async () => {
    const visit = await open({ ...IDLE, isConfigured: false });

    expect(texts(visit.page, '[data-dormant]')).toEqual(['admin.prerender.dormant']);
    expect(texts(visit.page, '[data-launch-lock]')).toEqual([
      'admin.prerender.launch.locked_dormant',
    ]);
    expect(launchButton(visit)?.disabled).toBe(true);
  });

  it('tells to set the served epoch and redeploy once a newer one is complete', async () => {
    const [completed] = prerenderReport().passes;
    const done = prerenderReport({
      current: null,
      readyEpoch: 3,
      passes: [{ ...completed, epoch: 3, state: 'completed' }],
    });
    const { page } = await openPanelInFrench(PrerenderPanel, URL, [
      { path: PRERENDER, body: done },
    ]);

    expect(texts(page, '[data-ready-epoch]')).toEqual([
      "L'epoch 3 est complète. Pour la servir, posez LODB_ARCHIVE_SERVE_EPOCH=3 et " +
        "redéployez : nginx ne change pas d'epoch à chaud.",
    ]);
    expect(page.querySelector('[data-ready-epoch]')?.getAttribute('role')).toBe('status');
  });

  it('says nothing of a switch while no newer epoch is complete', async () => {
    const { page } = await open(IDLE);

    expect(page.querySelector('[data-ready-epoch]')).toBeNull();
    expect(page.querySelector('[data-dormant]')).toBeNull();
  });

  it('reads itself again while a pass runs, and stops once it ends', async () => {
    configureAdminTestBed(
      [panelRoute(PrerenderPanel, URL)],
      [{ provide: PRERENDER_POLL, useValue: SPEC_POLL_MS }],
    );
    const visit = await visitPanel(URL, [{ path: PRERENDER, body: prerenderReport() }]);

    // Not rendered in between: while a pass runs, a read is always due, and the page never
    // turns stable.
    (await sent(visit, PRERENDER)).flush(withPass('running'));
    await readAgain(visit, IDLE);
    await new Promise((resolve) => setTimeout(resolve, SPEC_WAIT_MS));

    expect(visit.http.match(`${ADMIN_API}${PRERENDER}`)).toHaveLength(0);
    expect(texts(visit.page, '[data-no-pass]')).toEqual(['admin.prerender.current.none']);
  });

  it('keeps reading after a read fails while a pass runs', async () => {
    configureAdminTestBed(
      [panelRoute(PrerenderPanel, URL)],
      [{ provide: PRERENDER_POLL, useValue: SPEC_POLL_MS }],
    );
    const visit = await visitPanel(URL, [{ path: PRERENDER, body: prerenderReport() }]);

    // The API redeploys: one read answers 502, the next one finds the pass ended.
    (await sent(visit, PRERENDER)).flush(null, BAD_GATEWAY);
    await readAgain(visit, IDLE);
    await new Promise((resolve) => setTimeout(resolve, SPEC_WAIT_MS));

    expect(visit.http.match(`${ADMIN_API}${PRERENDER}`)).toHaveLength(0);
    expect(texts(visit.page, '[data-no-pass]')).toEqual(['admin.prerender.current.none']);
  });

  it('reads in French, the states and the types of page by their names', async () => {
    const { page } = await openPanelInFrench(PrerenderPanel, URL, [
      { path: PRERENDER, body: prerenderReport() },
    ]);

    expect(texts(page, '[data-pass-state]')).toEqual(['en cours']);
    expect(texts(page, 'tr[data-epoch="1"] td')[1]).toBe('retirée');
    expect(texts(page, 'tr[data-pass="2"] td')[3]).toContain('objets');
    expect(texts(page, 'lodb-current-pass-card button')[0]).toBe('Mettre en pause');
    expect(texts(page, 'lodb-kpi')[3]).toContain(
      "+ < 0,01 € pour les epochs retirées, jusqu'à leur suppression",
    );
  });
});
