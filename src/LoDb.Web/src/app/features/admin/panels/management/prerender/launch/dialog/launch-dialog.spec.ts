import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpTestingController } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { notice } from '../../../../../testing/dom/notice';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { sent } from '../../../../../testing/http/sent';
import { ADMIN_TIMEOUT } from '../../../../../shared/http/admin-timeout';
import type { LaunchPreset } from '../../common/scope/launch-preset';
import { LaunchDialog } from './launch-dialog';
import type { LaunchData } from './launch-dialog-store';
import { LaunchOpener } from './launch-opener';

const PATCHES = '/api/admin/prerender/patches';
const PASSES = '/api/admin/prerender/passes';
const IDLE = prerenderReport({ current: null });
const STARTED = { id: 4, epoch: 2, units: 16, skippedUnits: 5, ignoredVersions: ['3.15.9'] };
const HEADER: LaunchPreset = {
  origin: 'header',
  versions: [],
  priority: [],
  locales: null,
  pageTypes: null,
  epoch: null,
  skipComplete: true,
  fromPass: null,
};
const COMPLETE: LaunchPreset = { ...HEADER, origin: 'complete-epoch', versions: 'incomplete' };
const API = { http: null as unknown as HttpTestingController };

type Fixture = ComponentFixture<LaunchDialog>;

function providers(preset: LaunchPreset, ref: { close: (value?: unknown) => void }) {
  const data: LaunchData = { preset, report: IDLE };
  return [
    { provide: DIALOG_DATA, useValue: data },
    { provide: DialogRef, useValue: ref },
    { provide: ADMIN_TIMEOUT, useValue: 200 },
  ];
}

// An answer settles through a few promises before the dialog renders it.
async function settle(fixture: Fixture): Promise<void> {
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve));
  await fixture.whenStable();
  fixture.detectChanges();
}

async function open(preset: LaunchPreset = HEADER) {
  const ref = { close: vi.fn() };
  configureAdminTestBed([], providers(preset, ref));
  API.http = TestBed.inject(HttpTestingController);
  const fixture = TestBed.createComponent(LaunchDialog);
  fixture.detectChanges();
  (await sent(API, PATCHES)).flush(prerenderPatches());
  await settle(fixture);
  return { fixture, ref };
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

async function press(fixture: Fixture, selector: string): Promise<void> {
  button(fixture, selector).click();
  await settle(fixture);
}

function current(fixture: Fixture): string | null {
  return page(fixture).querySelector('[aria-current="step"]')?.getAttribute('data-step') ?? null;
}

async function launch(fixture: Fixture) {
  button(fixture, '[data-launch]').click();
  fixture.detectChanges();
  return sent(API, PASSES, 'POST');
}

describe('LaunchDialog', () => {
  it('goes forward once a step is valid, and back at any time', async () => {
    const { fixture } = await open();
    expect(current(fixture)).toBe('1');
    expect(button(fixture, '[data-next]').disabled).toBe(true);
    expect(button(fixture, '[data-step="3"]').disabled).toBe(true);
    // With nothing ticked, the footer says why « Suivant » waits, and the button points to it.
    const footer = page(fixture).querySelector('[data-launch-footer]');
    expect(footer?.textContent?.trim()).toBe('admin.prerender.launcher.footer_empty');
    expect(button(fixture, '[data-next]').getAttribute('aria-describedby')).toBe(footer?.id);

    page(fixture).querySelector<HTMLElement>('[data-key="16.18.1"]')?.click();
    await settle(fixture);
    await press(fixture, '[data-next]');
    expect(current(fixture)).toBe('2');
    expect(page(fixture).querySelectorAll('[data-locale][aria-pressed="true"]')).toHaveLength(21);

    await press(fixture, '[data-next]');
    expect(current(fixture)).toBe('3');
    expect(page(fixture).querySelectorAll('[data-tile]')).toHaveLength(5);

    await press(fixture, '[data-step="1"]');
    expect(current(fixture)).toBe('1');
    expect(page(fixture).querySelector('[data-launch-footer]')?.textContent?.trim()).toBe(
      'admin.prerender.launcher.footer',
    );
    expect(button(fixture, '[data-next]').hasAttribute('aria-describedby')).toBe(false);
  });

  it('keeps the focus on the forward button when the footer changes its buttons', async () => {
    const { fixture } = await open({ ...HEADER, origin: 'coverage', versions: ['16.18.1'] });
    button(fixture, '[data-next]').focus();

    await press(fixture, '[data-next]');
    expect(current(fixture)).toBe('3');
    expect(document.activeElement).toBe(button(fixture, '[data-launch]'));

    await press(fixture, '[data-back]');
    await press(fixture, '[data-back]');
    expect(current(fixture)).toBe('1');
    expect(document.activeElement).toBe(button(fixture, '[data-next]'));
  });

  it('blocks the perimeter without a locale', async () => {
    const { fixture } = await open({ ...HEADER, origin: 'coverage', versions: ['16.18.1'] });
    expect(current(fixture)).toBe('2');

    const none = [...page(fixture).querySelectorAll('button')].find(
      (candidate) => candidate.textContent?.trim() === 'admin.prerender.launcher.locales.none',
    );
    none?.click();
    await settle(fixture);

    expect(page(fixture).querySelector('[data-locales-required]')).not.toBeNull();
    expect(button(fixture, '[data-next]').disabled).toBe(true);
  });

  it('reads the coverage of the epoch chosen', async () => {
    const { fixture } = await open({ ...HEADER, origin: 'coverage', versions: ['16.18.1'] });

    page(fixture).querySelector<HTMLInputElement>('[data-epoch="3"] input')?.click();
    fixture.detectChanges();

    const read = await sent(API, PATCHES);
    expect(read.request.params.get('epoch')).toBe('3');
    read.flush(prerenderPatches({ epoch: 3 }));
    await settle(fixture);
    expect(page(fixture).querySelector('[data-skip]')).toBeNull();
  });

  it('launches what it summed up, then closes on the pass', async () => {
    const { fixture, ref } = await open(COMPLETE);
    expect(current(fixture)).toBe('3');
    expect(button(fixture, '[data-launch]').textContent).toContain(
      'admin.prerender.launcher.nav.launch',
    );

    const call = await launch(fixture);
    expect(call.request.body).toEqual({
      epoch: 2,
      versions: ['16.18.1'],
      locales: null,
      pageTypes: null,
      priority: null,
      skipComplete: true,
    });
    await settle(fixture);
    expect(button(fixture, '[data-launch]').disabled).toBe(true);
    call.flush(STARTED, { status: 201, statusText: 'Created' });
    await settle(fixture);

    expect(ref.close).toHaveBeenCalledWith(STARTED);
  });

  it('goes back to the step a refusal names, with its message', async () => {
    const { fixture } = await open(COMPLETE);

    (await launch(fixture)).flush(
      { code: 'validation-failed', errors: { epoch: ['retired'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await settle(fixture);

    expect(current(fixture)).toBe('2');
    expect(page(fixture).querySelector('[data-field-error]')?.textContent?.trim()).toBe(
      'admin.prerender.launcher.errors.epoch.retired',
    );
  });

  it('raises a band once another pass is active, the launch locked', async () => {
    const { fixture, ref } = await open(COMPLETE);

    (await launch(fixture)).flush(
      { code: 'prerender-pass-active' },
      { status: 409, statusText: 'Conflict' },
    );
    await settle(fixture);

    const band = page(fixture).querySelector('[data-band="active"]');
    expect(band?.textContent).toContain('admin.prerender.launcher.conflict.active');
    expect(button(fixture, '[data-launch]').disabled).toBe(true);

    band?.querySelector('button')?.click();
    expect(ref.close).toHaveBeenCalledWith(null);
  });

  it('offers to check once the API did not answer', async () => {
    const { fixture, ref } = await open(COMPLETE);

    await launch(fixture);
    await new Promise((resolve) => setTimeout(resolve, 250));
    await settle(fixture);
    const band = page(fixture).querySelector('[data-band="network"]');
    expect(band?.textContent).toContain('admin.prerender.launcher.network');

    band?.querySelector('button')?.click();
    const newer = { ...(prerenderReport().current as PrerenderPassView), id: 4 };
    (await sent(API, '/api/admin/prerender')).flush(prerenderReport({ current: newer }));
    await settle(fixture);

    expect(ref.close).toHaveBeenCalledWith(expect.objectContaining({ id: 4 }));
  });
});

describe('LaunchOpener', () => {
  afterEach(() => document.querySelector('.cdk-overlay-container')?.replaceChildren());

  it('opens the dialog, and posts the notice of the pass it started', async () => {
    configureAdminTestBed([], [{ provide: ADMIN_TIMEOUT, useValue: 200 }]);
    API.http = TestBed.inject(HttpTestingController);
    const opening = TestBed.runInInjectionContext(() =>
      TestBed.inject(LaunchOpener).open(COMPLETE, IDLE),
    );
    const app = TestBed.inject(ApplicationRef);
    app.tick();
    (await sent(API, PATCHES)).flush(prerenderPatches());
    await app.whenStable();
    app.tick();

    const overlay = document.querySelector('.cdk-overlay-container');
    expect(overlay?.querySelector('lodb-launch-dialog')?.classList).toContain('admin-overlay');
    overlay?.querySelector<HTMLButtonElement>('[data-launch]')?.click();
    (await sent(API, PASSES, 'POST')).flush(STARTED, { status: 201, statusText: 'Created' });

    await expect(opening).resolves.toEqual(STARTED);
    expect(notice()).toBe(
      'notice: admin.prerender.launcher.done admin.prerender.launcher.done_ignored',
    );
  });
});
