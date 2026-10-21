import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import { notice } from '../../../../testing/dom/notice';
import { prerenderProgress } from '../../../../testing/fixtures/prerender-progress';
import { sent } from '../../../../testing/http/sent';
import { MonitorHarness } from './live-monitor-harness';

const PASSES = '/api/admin/prerender/passes';
const BASE = prerenderProgress();
const REASON = 'The store refused the writer key (403).';

function click(root: ParentNode, selector: string): void {
  const button = root.querySelector<HTMLButtonElement>(selector);
  if (!button) {
    throw new Error(`No button matches ${selector}.`);
  }
  button.click();
}

// Opens the question of `action`, then answers it with the button `answer`.
async function answer(monitor: MonitorHarness, action: string, choice: string): Promise<string> {
  click(monitor.page, action);
  // Rendered once its labels are written, not only mounted.
  await monitor.until(
    () => !!document.querySelector(`.cdk-overlay-container ${choice}`)?.textContent?.trim(),
  );
  const words = (monitor.overlay().textContent ?? '').replace(/\s+/g, ' ');
  click(monitor.overlay(), choice);
  await monitor.settle();
  return words;
}

// How the last move went, said beside the buttons pressed rather than at the top of the page.
function outcome(monitor: MonitorHarness): string {
  return monitor.text('[data-action-outcome]');
}

// The outcome is written once its words are translated, a few turns after the answer.
async function said(monitor: MonitorHarness): Promise<string> {
  await monitor.until(() => outcome(monitor) !== '');
  return outcome(monitor);
}

async function openPaused(pass: PrerenderPassView): Promise<MonitorHarness> {
  const monitor = MonitorHarness.open(pass);
  await monitor.read(prerenderProgress({ isComplete: true, pass }));
  return monitor;
}

describe('LiveMonitor actions', () => {
  afterEach(() => document.querySelector('.cdk-overlay-container')?.replaceChildren());

  it('says what a pause does, then pauses and reads again', async () => {
    const monitor = MonitorHarness.open();
    await monitor.read();

    const words = await answer(monitor, '[data-action="pause"]', '[data-confirm]');
    expect(words).toContain('admin.prerender.live.confirm.pause_title');
    expect(words).toContain('admin.prerender.live.confirm.pause_current');
    (await sent(monitor, `${PASSES}/12/pause`, 'POST')).flush('');
    await monitor.read(BASE);

    expect(await said(monitor)).toBe('admin.prerender.done.pause');
    expect(notice()).toBe('');
    expect(monitor.host.changes).toBe(1);
  });

  it('says a refusal beside the buttons, then shows where the pass is now', async () => {
    const monitor = MonitorHarness.open();
    await monitor.read();

    await answer(monitor, '[data-action="pause"]', '[data-confirm]');
    (await sent(monitor, `${PASSES}/12/pause`, 'POST')).flush(
      { type: 'about:blank', code: 'prerender-pass-state' },
      { status: 409, statusText: 'Conflict' },
    );
    await monitor.settle();

    await said(monitor);
    expect(monitor.page.querySelector('.monitor-outcome--bad')).not.toBeNull();
    expect(notice()).toBe('');
    expect(monitor.host.changes).toBe(1);
  });

  it('keeps the pass running when the pause is not confirmed', async () => {
    const monitor = MonitorHarness.open();
    await monitor.read();

    await answer(monitor, '[data-action="pause"]', '[data-confirm-keep]');

    monitor.http.expectNone((request) => request.method === 'POST');
    expect(monitor.host.changes).toBe(0);
  });

  it('confirms a cancel, then cancels', async () => {
    const monitor = MonitorHarness.open();
    await monitor.read();

    const words = await answer(monitor, '[data-action="cancel"]', '[data-confirm]');
    expect(words).toContain('admin.prerender.live.confirm.cancel_partial');
    (await sent(monitor, `${PASSES}/12/cancel`, 'POST')).flush('');
    await monitor.settle();

    expect(await said(monitor)).toBe('admin.prerender.done.cancel');
    expect(notice()).toBe('');
  });

  it('resumes a pause of an administrator at once', async () => {
    const monitor = await openPaused({ ...BASE.pass, state: 'paused' });

    click(monitor.page, '[data-action="resume"]');
    (await sent(monitor, `${PASSES}/12/resume`, 'POST')).flush('');
    await monitor.settle();

    expect(await said(monitor)).toBe('admin.prerender.done.resume');
    expect(notice()).toBe('');
  });

  it('warns before resuming a pass that paused itself', async () => {
    const monitor = await openPaused({ ...BASE.pass, state: 'paused', pauseReason: REASON });

    const words = await answer(monitor, '[data-paused-itself] button', '[data-confirm]');
    expect(words).toContain('admin.prerender.live.confirm.resume_warning');
    (await sent(monitor, `${PASSES}/12/resume`, 'POST')).flush('');
    await monitor.settle();

    expect(await said(monitor)).toBe('admin.prerender.done.resume');
    expect(notice()).toBe('');
  });

  it('opens the units of a version from the unit running', async () => {
    const monitor = MonitorHarness.open();
    await monitor.read();

    click(monitor.page, 'lodb-now-block [data-now-unit] button');
    await monitor.until(
      () => document.querySelector('.cdk-overlay-container lodb-version-units-dialog') !== null,
    );

    await sent(monitor, `${PASSES}/12/versions/16.18.1`);
  });
});
