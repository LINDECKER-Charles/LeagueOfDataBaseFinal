import type { PrerenderEpochCoverage } from '../../../../../core/api/generated/models/prerender-epoch-coverage';
import type { PrerenderPassView } from '../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../core/api/generated/models/prerender-report';
import type { AdminVisit } from '../../../testing/admin-visit';
import { press } from '../../../testing/dom/press';
import { prerenderPatches } from '../../../testing/fixtures/prerender-patches';
import { prerenderProgress } from '../../../testing/fixtures/prerender-progress';
import { prerenderReport } from '../../../testing/fixtures/prerender-report';
import { sent } from '../../../testing/http/sent';
import { settle } from '../../../testing/http/settle';
import type { PanelCall } from '../../../testing/panels/panel-call';
import type { LaunchPreset } from './common/scope/launch-preset';
import { LaunchOpener } from './launch/dialog/launch-opener';
import { openPrerender } from './prerender-panel-harness';

const PRERENDER = '/api/admin/prerender';
const PATCHES = `${PRERENDER}/patches`;
const IDLE = prerenderReport({ current: null, passes: prerenderReport().passes.slice(1) });
const PASS = prerenderProgress().pass;
const RUNNING = prerenderReport({ current: PASS, passes: [PASS] });
const PROGRESS = { path: `${PRERENDER}/passes/12/progress`, body: prerenderProgress() };
const COVERAGE = IDLE.storage.epochs[0].coverage as PrerenderEpochCoverage;
const DEFAULTS = { priority: [], locales: null, pageTypes: null, fromPass: null };

// The launch dialog stands aside: the spec reads what the page asks it to open.
function opener() {
  const open = vi.fn((_preset: LaunchPreset, _report: PrerenderReport) => Promise.resolve(null));
  return { open, providers: [{ provide: LaunchOpener, useValue: { open } }] };
}

async function openWith(report: PrerenderReport, calls: readonly PanelCall[] = []) {
  const fake = opener();
  const visit = await openPrerender({ report, calls, providers: fake.providers });
  return { visit, open: fake.open };
}

function click(visit: AdminVisit, selector: string, init: MouseEventInit = {}): void {
  const target = visit.page.querySelector<HTMLElement>(selector);
  if (!target) {
    throw new Error(`Nothing matches ${selector}.`);
  }
  target.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, ...init }));
  visit.harness.detectChanges();
}

function presetOf(open: ReturnType<typeof opener>['open']): LaunchPreset | undefined {
  return open.mock.lastCall?.[0];
}

async function settled(visit: AdminVisit): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve));
  await settle(visit);
}

describe('PrerenderPanel, its views and presets', () => {
  it('shows six views, the pass first, each named by its tile', async () => {
    const { visit } = await openWith(IDLE);
    const tabs = [...visit.page.querySelectorAll('[role="tab"]')];
    const name = (tab: Element) =>
      visit.page.querySelector(`#${tab.getAttribute('aria-labelledby')}`)?.textContent?.trim();

    expect(tabs.map(name)).toEqual([
      'admin.prerender.overview.views.pass.label',
      'admin.prerender.overview.views.coverage.label',
      'admin.prerender.overview.views.queues.label',
      'admin.prerender.overview.views.speed.label',
      'admin.prerender.overview.views.storage.label',
      'admin.prerender.overview.views.history.label',
    ]);
    expect(tabs[0].getAttribute('aria-selected')).toBe('true');
  });

  it('opens an empty launch from the head, then reads the page again', async () => {
    const { visit, open } = await openWith(IDLE);

    press(visit, 'admin.prerender.overview.cta.launch');
    // Closed without a pass: the page reads its report and its coverage again all the same.
    await sent(visit, PRERENDER);
    await sent(visit, PATCHES);

    expect(presetOf(open)).toEqual({
      ...DEFAULTS,
      origin: 'header',
      versions: [],
      epoch: null,
      skipComplete: true,
    });
    expect(open.mock.lastCall?.[1]).toEqual(IDLE);
  });

  it('completes the gaps of the served epoch from the card without a pass', async () => {
    const { visit, open } = await openWith(IDLE);

    press(visit, 'admin.prerender.overview.idle.gaps_cta');

    expect(presetOf(open)).toMatchObject({ origin: 'idle', versions: 'incomplete', epoch: null });
  });

  it('completes the standby epoch nearly ready from its band', async () => {
    const standby = { ...IDLE.storage.epochs[0], epoch: 3, state: 'standby', isWriting: false };
    const nearly = { ...COVERAGE, gapUnits: 300, completeUnits: 8_142 };
    const epochs = [{ ...standby, coverage: nearly }, ...IDLE.storage.epochs];
    const { visit, open } = await openWith({ ...IDLE, storage: { ...IDLE.storage, epochs } });

    press(visit, 'admin.prerender.overview.bands.nearly_cta');

    expect(presetOf(open)).toMatchObject({
      origin: 'complete-epoch',
      versions: 'incomplete',
      epoch: 3,
    });
  });

  it('pre-renders the patches ticked in the coverage, a Shift range included', async () => {
    const { visit, open } = await openWith(IDLE);

    click(visit, '[data-version="16.19.1"] input');
    click(visit, '[data-version="16.18.1"] input', { shiftKey: true });
    click(visit, '[data-launch-selection]');

    expect(presetOf(open)).toEqual({
      ...DEFAULTS,
      origin: 'coverage',
      versions: ['16.19.1', '16.18.1'],
      epoch: 2,
      skipComplete: true,
    });
  });

  it('completes the gaps of the epoch read in the coverage, and of an epoch of the store', async () => {
    const { visit, open } = await openWith(IDLE);

    click(visit, '[data-complete-gaps]');
    expect(presetOf(open)).toMatchObject({ origin: 'complete-epoch', epoch: 2 });

    click(visit, 'tr[data-epoch="2"] [data-complete]');
    expect(presetOf(open)).toMatchObject({ origin: 'complete-epoch', epoch: 2 });
    expect(open).toHaveBeenCalledTimes(2);
  });

  it('holds every launch while a pass runs', async () => {
    const { visit, open } = await openWith(RUNNING, [PROGRESS]);

    click(visit, '[data-version="16.18.1"] input');

    expect(visit.page.querySelector<HTMLButtonElement>('[data-complete-gaps]')?.disabled).toBe(
      true,
    );
    expect(visit.page.querySelector<HTMLButtonElement>('[data-launch-selection]')?.disabled).toBe(
      true,
    );
    expect(open).not.toHaveBeenCalled();
  });

  it('completes the epoch of a pass cancelled from its recap', async () => {
    const cancelled: PrerenderPassView = {
      ...PASS,
      state: 'cancelled',
      finishedAt: '2026-10-20T10:00:00Z',
    };
    const { visit, open } = await openWith({ ...IDLE, passes: [cancelled] });

    press(visit, 'admin.prerender.live.recap.complete');

    expect(presetOf(open)).toMatchObject({ origin: 'complete-epoch', epoch: 2 });
  });

  it('relaunches a perimeter from the detail of a pass of the history', async () => {
    const { visit, open } = await openWith(IDLE);

    click(visit, 'tr[data-pass="2"] th button');
    (await sent(visit, `${PRERENDER}/passes/2/progress`)).flush(
      prerenderProgress({ pass: IDLE.passes[0], isComplete: true }),
    );
    (await sent(visit, PATCHES)).flush(prerenderPatches());
    await settled(visit);
    document
      .querySelector<HTMLButtonElement>('lodb-pass-detail-dialog [data-relaunch-scope]')
      ?.click();
    await vi.waitFor(() => expect(open).toHaveBeenCalledOnce());

    expect(presetOf(open)).toMatchObject({
      origin: 'relaunch',
      locales: ['fr'],
      pageTypes: ['items'],
      epoch: 2,
      skipComplete: false,
      fromPass: 2,
    });
  });
});
