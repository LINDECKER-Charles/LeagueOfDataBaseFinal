import { HttpStatusCode } from '@angular/common/http';
import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import { prerenderProgress } from '../../../../testing/fixtures/prerender-progress';
import { MonitorHarness } from './live-monitor-harness';

const BASE = prerenderProgress();
const BAD_GATEWAY = { status: HttpStatusCode.BadGateway, statusText: 'Bad Gateway' };
const REASON = 'The store refused the writer key (403).';
const MINUTE = 60_000;

function paused(changes: Partial<PrerenderPassView> = {}): PrerenderPassView {
  return { ...BASE.pass, state: 'paused', pausedAt: '2026-10-21T06:30:00+00:00', ...changes };
}

// The pass paused while 16.18.1 · fr ran: the unit stays running in the base, a z cell.
function pausedProgress(pass: PrerenderPassView) {
  return prerenderProgress({
    isComplete: true,
    pass,
    versions: [{ ...BASE.versions[0], cells: 'ccwcccz..............' }],
  });
}

function cell(monitor: MonitorHarness, locale: string): string | null {
  const found = monitor.page.querySelector(`.cell[data-v="16.18.1"][data-l="${locale}"]`);
  return found?.getAttribute('data-c') ?? null;
}

function freshness(monitor: MonitorHarness): string | null {
  return monitor.page.querySelector('.live-dot')?.getAttribute('data-freshness') ?? null;
}

describe('LiveMonitor', () => {
  afterEach(() => vi.useRealTimers());

  it('shows a running pass: its share, the unit running, what comes next, its units', async () => {
    const monitor = MonitorHarness.open();
    await monitor.read();

    expect(monitor.text('h2')).toBe('admin.prerender.live.title');
    expect(monitor.text('[data-pass-state]')).toBe('admin.prerender.states.running');
    expect(monitor.text('[data-share]')).toBe('71.4 %');
    expect(monitor.text('[data-pages]')).toBe('admin.prerender.live.pages_estimated');
    expect(monitor.text('[data-units]')).toBe('admin.prerender.live.units');
    expect(monitor.text('lodb-now-block h3')).toBe('admin.prerender.live.now.title');
    expect(monitor.text('[data-now-line]')).toBe('admin.prerender.live.now.unit');
    expect(monitor.page.querySelectorAll('[data-now-next] .now-chip')).toHaveLength(2);
    expect(monitor.page.querySelector('[data-now-wait]')).not.toBeNull();
    expect(cell(monitor, 'fr')).toBe('r');
    expect(monitor.page.querySelector('[data-stall]')).toBeNull();
    expect(monitor.page.querySelector('[data-event="done"]')).not.toBeNull();
    expect(monitor.text('[data-failures-summary]')).toContain('live.failures.summary');
    expect(monitor.page.querySelector<HTMLButtonElement>('[data-relaunch]')?.disabled).toBe(true);
    expect(monitor.page.querySelector('[data-action="pause"]')).not.toBeNull();
    expect(monitor.page.querySelector('[data-action="resume"]')).toBeNull();
  });

  it('shows the report figures before the first read', () => {
    const monitor = MonitorHarness.open();

    expect(monitor.text('[data-share]')).toBe('71.4 %');
    expect(monitor.page.querySelector('lodb-now-block')).toBeNull();
  });

  it('reads only what changed since the last answer, less 30 seconds', async () => {
    const monitor = MonitorHarness.open();
    const first = await monitor.read();
    const second = await monitor.read(BASE);

    expect(first.request.params.get('since')).toBeNull();
    expect(second.request.params.get('since')).toBe('2026-10-21T08:41:35.000Z');
  });

  it('shows a pass an administrator paused: where it resumes, no stall', async () => {
    const monitor = MonitorHarness.open(paused());
    await monitor.read(pausedProgress(paused()));

    expect(monitor.text('[data-pass-state]')).toBe('admin.prerender.states.paused');
    expect(monitor.text('[data-paused-since]')).toBe('admin.prerender.live.paused_since');
    expect(monitor.text('lodb-now-block h3')).toBe('admin.prerender.live.now.resumes_with');
    expect(monitor.text('[data-now-line]')).toBe('admin.prerender.live.now.stopped');
    expect(monitor.page.querySelector('[data-now-since]')).toBeNull();
    expect(monitor.page.querySelector('[data-now-wait]')).toBeNull();
    expect(cell(monitor, 'fr')).toBe('z');
    expect(monitor.page.querySelector('[data-stall]')).toBeNull();
    expect(monitor.page.querySelector('[data-paused-itself]')).toBeNull();
    expect(monitor.text('[data-readout="pace"] .readout-value')).toBe(
      'admin.prerender.live.pace.stopped',
    );
    expect(monitor.page.querySelector('.monitor-matrix--paused')).not.toBeNull();
    expect(monitor.text('[data-units]')).toBe('admin.prerender.live.units_paused');
    expect(monitor.text('[data-action="resume"]')).toBe('admin.prerender.live.actions.resume');
  });

  it('says why a pass paused itself, in a band that resumes it', async () => {
    const pass = paused({ pauseReason: REASON });
    const monitor = MonitorHarness.open(pass);
    await monitor.read(pausedProgress(pass));

    expect(monitor.text('[data-paused-itself]')).toContain('admin.prerender.live.paused_itself');
    expect(monitor.text('[data-action="resume"]')).toBe(
      'admin.prerender.live.actions.resume_itself',
    );
  });

  it('says when the unit running fails in numbers, with its reason and a way to pause', async () => {
    const monitor = MonitorHarness.open();
    const unit = { ...BASE.running[0], pagesWritten: 6, pagesSkipped: 0, pagesFailed: 8 };
    await monitor.read(prerenderProgress({ running: [{ ...unit, failure: REASON }] }));

    expect(monitor.text('[data-failing-line]')).toBe('admin.prerender.live.failing');
    expect(monitor.text('[data-failing-reason]')).toBe(REASON);
    expect(monitor.page.querySelector('[data-failing-pause]')).not.toBeNull();
  });

  it('keeps the figures through a 502, then says it is live again', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    const monitor = MonitorHarness.open();
    await monitor.read();
    expect(freshness(monitor)).toBe('live');

    (await monitor.next()).flush('', BAD_GATEWAY);
    await monitor.settle();
    vi.setSystemTime(Date.now() + 2 * MINUTE);
    await monitor.until(() => freshness(monitor) === 'lost');
    expect(monitor.text('[data-share]')).toBe('71.4 %');

    await monitor.read(BASE);
    expect(freshness(monitor)).toBe('live');
  });

  it('holds its share when the total grows, and says the total was re-estimated', async () => {
    const monitor = MonitorHarness.open();
    await monitor.read();
    const pagesLeft = (BASE.pass.pagesLeft ?? 0) + 300_000;
    await monitor.read(prerenderProgress({ pass: { ...BASE.pass, pagesLeft } }));

    expect(monitor.text('[data-share]')).toBe('71.4 %');
    expect(monitor.page.querySelector('[data-event="total_up"]')).not.toBeNull();
  });

  it('says once the pass ended', async () => {
    const monitor = MonitorHarness.open();
    await monitor.read();
    await monitor.read(prerenderProgress({ pass: { ...BASE.pass, state: 'completed' } }));

    expect(monitor.host.ends).toEqual([12]);
  });

  it('asks the report again when the API no longer knows the pass', async () => {
    const monitor = MonitorHarness.open();
    (await monitor.next()).flush('', { status: HttpStatusCode.NotFound, statusText: 'Not Found' });
    await monitor.settle();

    expect(monitor.host.changes).toBe(1);
  });
});
