import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpTestingController } from '@angular/common/http/testing';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import { prerenderPatches } from '../../../../testing/fixtures/prerender-patches';
import { prerenderProgress } from '../../../../testing/fixtures/prerender-progress';
import { prerenderReport } from '../../../../testing/fixtures/prerender-report';
import { sent } from '../../../../testing/http/sent';
import { type PassDetailData, PassDetailDialog } from './pass-detail-dialog';

const PATCHES = '/api/admin/prerender/patches';
const ACTIVE = prerenderProgress().pass;
// Pass 11, ended: three listed versions, one of them failed in two locales.
const ENDED: PrerenderPassView = {
  ...ACTIVE,
  id: 11,
  state: 'completed',
  finishedAt: '2026-10-17T22:14:00Z',
  scope: { ...ACTIVE.scope, versionCount: 3, newestListed: '16.18.1', oldestListed: '15.2.1' },
};
const IDLE = prerenderReport({ current: null, passes: [ENDED] });
const RUNNING = prerenderReport({ current: ACTIVE, passes: [ACTIVE, ENDED] });
const SCOPE_VERSIONS = ['16.18.1', '16.12.1', '15.2.1'];

type Fixture = ComponentFixture<PassDetailDialog>;

async function open(data: PassDetailData) {
  const close = vi.fn();
  configureAdminTestBed(
    [],
    [
      { provide: DIALOG_DATA, useValue: data },
      { provide: DialogRef, useValue: { close } },
    ],
  );
  const api = { http: TestBed.inject(HttpTestingController) };
  const fixture = TestBed.createComponent(PassDetailDialog);
  fixture.detectChanges();
  return { fixture, api, close };
}

async function read(fixture: Fixture, api: { http: HttpTestingController }, id = 11) {
  (await sent(api, `/api/admin/prerender/passes/${id}/progress`)).flush(
    prerenderProgress({
      pass: { ...ENDED, id },
      isComplete: true,
      scopeVersions: SCOPE_VERSIONS,
      versions: [{ ...prerenderProgress().versions[0], cells: 'ccfcccccccccccccwcccc' }],
    }),
  );
  await fixture.whenStable();
  fixture.detectChanges();
}

function page(fixture: Fixture): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function button(fixture: Fixture, selector: string): HTMLButtonElement {
  const found = page(fixture).querySelector<HTMLButtonElement>(selector);
  if (!found) {
    throw new Error(`No button matches ${selector}.`);
  }
  return found;
}

describe('PassDetailDialog', () => {
  it('reads the progress of the pass once, whole, and shows its matrix and failures', async () => {
    const { fixture, api } = await open({ pass: ENDED, report: IDLE });
    expect(page(fixture).querySelector('[data-detail-loading]')).not.toBeNull();

    await read(fixture, api);
    await new Promise((resolve) => setTimeout(resolve, 30));

    expect(page(fixture).querySelector('lodb-unit-matrix')).not.toBeNull();
    expect(page(fixture).querySelector('lodb-failures-list')).not.toBeNull();
    expect(page(fixture).querySelectorAll('[data-detail-outcome] li')).toHaveLength(5);
    api.http.expectNone(() => true);
  });

  it('lists the versions of the perimeter from the complete read', async () => {
    const { fixture, api } = await open({ pass: ENDED, report: IDLE });
    await read(fixture, api);

    button(fixture, '[data-detail-scope] button').click();
    fixture.detectChanges();

    const chips = [...page(fixture).querySelectorAll('[data-scope-versions] li')];
    expect(chips.map((chip) => chip.textContent?.trim())).toEqual(SCOPE_VERSIONS);
  });

  it('relaunches the failures, counted, or the whole perimeter', async () => {
    const { fixture, api, close } = await open({ pass: ENDED, report: IDLE });
    await read(fixture, api);

    expect(button(fixture, '[data-relaunch-failures]').textContent?.trim()).toBe(
      'admin.prerender.common.retry.units',
    );
    button(fixture, '[data-relaunch-failures]').click();
    expect(close).toHaveBeenLastCalledWith(
      expect.objectContaining({ origin: 'failures', versions: ['16.18.1'], fromPass: 11 }),
    );

    button(fixture, '[data-relaunch-scope]').click();
    expect(close).toHaveBeenLastCalledWith(
      expect.objectContaining({
        origin: 'relaunch',
        versions: SCOPE_VERSIONS,
        skipComplete: false,
      }),
    );
  });

  it('holds the relaunches while a pass is active, saying why', async () => {
    const { fixture, api } = await open({ pass: ENDED, report: RUNNING });
    await read(fixture, api);

    expect(button(fixture, '[data-relaunch-scope]').disabled).toBe(true);
    expect(page(fixture).querySelector('#prerender-pass-detail-lock')?.textContent).toContain(
      'admin.prerender.passes.detail.locked',
    );
  });

  it('follows the active pass live instead of relaunching it', async () => {
    const { fixture, api, close } = await open({ pass: ACTIVE, report: RUNNING });
    await read(fixture, api, 12);

    button(fixture, '[data-follow]').click();

    expect(close).toHaveBeenCalledWith('follow');
    expect(page(fixture).querySelector('[data-relaunch-scope]')).toBeNull();
  });

  it('turns a range from before the lists into the older patches it spans', async () => {
    const legacy: PrerenderPassView = {
      ...ENDED,
      scope: { ...ENDED.scope, versionCount: null, oldest: '16.18.1', newest: '16.19.1' },
    };
    const { fixture, api } = await open({ pass: legacy, report: IDLE });
    await read(fixture, api);
    (await sent(api, PATCHES)).flush(prerenderPatches());
    await fixture.whenStable();
    fixture.detectChanges();

    expect(page(fixture).querySelector('[data-legacy-range]')?.textContent).toContain(
      'admin.prerender.passes.detail.legacy_range',
    );
  });

  it('says the pass could not be read', async () => {
    const { fixture, api } = await open({ pass: ENDED, report: IDLE });
    (await sent(api, '/api/admin/prerender/passes/11/progress')).flush(null, {
      status: 502,
      statusText: 'Bad Gateway',
    });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(page(fixture).querySelector('[data-detail-error]')).not.toBeNull();
    expect(page(fixture).classList).toContain('admin-overlay');
  });
});
