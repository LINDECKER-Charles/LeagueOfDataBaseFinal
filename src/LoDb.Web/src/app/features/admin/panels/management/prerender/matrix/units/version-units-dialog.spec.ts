import { DIALOG_DATA } from '@angular/cdk/dialog';
import { HttpTestingController } from '@angular/common/http/testing';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderUnitView } from '../../../../../../../core/api/generated/models/prerender-unit-view';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { sent } from '../../../../../testing/http/sent';
import { PRERENDER_CADENCE } from '../../common/scope/prerender-cadence';
import { type VersionUnitsData, VersionUnitsDialog } from './version-units-dialog';

const UNITS = '/api/admin/prerender/passes/12/versions/16.18.1';
const CADENCE = {
  progressRunningMs: 10,
  progressPausedMs: 10,
  reportActiveMs: 10,
  reportIdleMs: 10,
  reportHiddenMs: 10,
  versionMs: 10,
};
const FAILED = prerenderProgress().failures[0];
const COMPLETED = prerenderProgress().recent[0];

// The 21 units of 16.18.1, all completed but de, which lost two pages.
function units(): PrerenderUnitView[] {
  return prerenderPatches().locales.map((locale) =>
    locale === 'de' ? FAILED : { ...COMPLETED, locale },
  );
}

async function open(data: VersionUnitsData) {
  configureAdminTestBed(
    [],
    [
      { provide: DIALOG_DATA, useValue: data },
      { provide: PRERENDER_CADENCE, useValue: CADENCE },
    ],
  );
  const fixture = TestBed.createComponent(VersionUnitsDialog);
  const api = { http: TestBed.inject(HttpTestingController) };
  fixture.detectChanges();
  return { fixture, api };
}

async function settle(fixture: ComponentFixture<VersionUnitsDialog>): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
}

function page(fixture: ComponentFixture<VersionUnitsDialog>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

describe('VersionUnitsDialog', () => {
  it('reads the units of the version once, a row per locale', async () => {
    const { fixture, api } = await open({ passId: 12, version: '16.18.1', isActive: false });
    expect(page(fixture).textContent).toContain('admin.prerender.common.units.loading');

    (await sent(api, UNITS)).flush({ passId: 12, version: '16.18.1', units: units() });
    await settle(fixture);
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(page(fixture).querySelectorAll('tbody tr')).toHaveLength(21);
    api.http.expectNone(`https://api.example.com${UNITS}`);
  });

  it('writes the reason of a failure in monospace English', async () => {
    const { fixture, api } = await open({ passId: 12, version: '16.18.1', isActive: false });
    (await sent(api, UNITS)).flush({ passId: 12, version: '16.18.1', units: units() });
    await settle(fixture);

    const failure = page(fixture).querySelector<HTMLElement>('[data-locale="de"] [data-failure]');
    expect(failure?.textContent?.trim()).toBe('The SSR answered 500, not a page to store.');
    expect(failure?.classList).toContain('font-mono');
    expect(failure?.getAttribute('lang')).toBe('en');
    expect(page(fixture).querySelectorAll('[data-failure]')).toHaveLength(1);
  });

  it('reads again while a unit runs in an active pass', async () => {
    const { fixture, api } = await open({ passId: 12, version: '16.18.1', isActive: true });
    const running = [{ ...prerenderProgress().running[0] }];
    (await sent(api, UNITS)).flush({ passId: 12, version: '16.18.1', units: running });
    await settle(fixture);

    (await sent(api, UNITS)).flush({ passId: 12, version: '16.18.1', units: [] });
    await settle(fixture);
    expect(page(fixture).querySelector('[data-units-empty]')).not.toBeNull();
  });
});
