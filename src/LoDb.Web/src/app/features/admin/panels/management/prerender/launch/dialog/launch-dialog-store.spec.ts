import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { HttpTestingController } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { prerenderPatches } from '../../../../../testing/fixtures/prerender-patches';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { sent } from '../../../../../testing/http/sent';
import { ADMIN_TIMEOUT } from '../../../../../shared/http/admin-timeout';
import type { LaunchPreset } from '../../common/scope/launch-preset';
import { type LaunchData, LaunchDialogStore } from './launch-dialog-store';

const PATCHES = '/api/admin/prerender/patches';
const PASSES = '/api/admin/prerender/passes';
const REPORT_PATH = '/api/admin/prerender';
// The report the dialog opens on: no active pass, the newest known is #3.
const IDLE = prerenderReport({ current: null });
const RUNNING = prerenderReport().current as PrerenderPassView;
const STARTED = { id: 4, epoch: 2, units: 16, skippedUnits: 5, ignoredVersions: [] };
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

function open(preset: LaunchPreset = HEADER) {
  const ref = { close: vi.fn() };
  const data: LaunchData = { preset, report: IDLE };
  configureAdminTestBed(
    [],
    [
      { provide: DIALOG_DATA, useValue: data },
      { provide: DialogRef, useValue: ref },
      { provide: ADMIN_TIMEOUT, useValue: 200 },
      LaunchDialogStore,
    ],
  );
  const store = TestBed.inject(LaunchDialogStore);
  const api = { http: TestBed.inject(HttpTestingController) };
  TestBed.tick();
  return { store, api, ref };
}

async function answer(
  api: { http: HttpTestingController },
  coverage: PrerenderPatchesReport = prerenderPatches(),
): Promise<void> {
  (await sent(api, PATCHES)).flush(coverage);
  await settle();
}

// The resource adopts its answer, then the effects run.
async function settle(): Promise<void> {
  await TestBed.inject(ApplicationRef).whenStable();
  TestBed.tick();
}

async function ready(preset: LaunchPreset = HEADER) {
  const opened = open(preset);
  await answer(opened.api);
  return opened;
}

describe('LaunchDialogStore', () => {
  it('opens on the step of its entry, then ticks the gaps once the coverage loads', async () => {
    const { store, api } = open({ ...HEADER, origin: 'complete-epoch', versions: 'incomplete' });
    expect(store.step()).toBe(3);
    expect(store.draft().isResolved).toBe(false);

    await answer(api);

    expect([...store.draft().selection.selected]).toEqual(['16.18.1']);
    expect(store.estimate()?.units).toBe(16);
  });

  it('goes forward only past valid steps, back at any time', async () => {
    const { store } = await ready();

    store.goTo(2);
    expect(store.step()).toBe(1);

    store.act({ kind: 'toggle', version: '16.18.1' });
    store.goTo(3);
    expect(store.step()).toBe(3);

    store.update({ locales: [] });
    store.goTo(1);
    store.goTo(3);
    expect(store.step()).toBe(1);
    store.goTo(2);
    expect(store.step()).toBe(2);
  });

  it('reads the coverage again when the epoch changes, the selection kept', async () => {
    const { store, api } = await ready({ ...HEADER, versions: ['16.18.1'] });

    store.chooseEpoch({ kind: 'new', epoch: 3, share: null, monthlyCost: 0, currency: 'EUR' });
    TestBed.tick();
    expect(store.coverage()).toBeNull();

    const read = await sent(api, PATCHES);
    expect(read.request.params.get('epoch')).toBe('3');
    read.flush(prerenderPatches({ epoch: 3 }));
    await settle();

    expect(store.coverage()?.epoch).toBe(3);
    expect([...store.draft().selection.selected]).toEqual(['16.18.1']);
    expect(store.draft().epochKind).toBe('new');
  });

  it('undoes a grouped selection', async () => {
    const { store } = await ready();

    store.act({ kind: 'set', versions: ['16.19.1', '16.18.1'], on: true });
    store.act({ kind: 'clear' });
    expect(store.draft().selection.selected.size).toBe(0);

    store.undoLast();
    expect(store.draft().selection.selected.size).toBe(2);
  });

  it('says the priorities were capped once a list added after others passes 100', async () => {
    const { store } = await ready();
    const versions = (from: number) =>
      Array.from({ length: 60 }, (_, index) => `14.${from + index}.1`);

    store.paste({ versions: versions(0), append: false, isCapped: false });
    expect(store.draft().isPriorityCapped).toBe(false);
    store.paste({ versions: versions(60), append: true, isCapped: false });

    expect(store.draft().selection.priority).toHaveLength(100);
    expect(store.draft().isPriorityCapped).toBe(true);
  });

  it('sends the draft, then closes on the pass started', async () => {
    const { store, api, ref } = await ready({ ...HEADER, versions: ['16.18.1'] });

    const launching = store.launch();
    const call = await sent(api, PASSES, 'POST');
    expect(call.request.body).toEqual({
      epoch: 2,
      versions: ['16.18.1'],
      locales: null,
      pageTypes: null,
      priority: null,
      skipComplete: true,
    });
    expect(store.isBusy()).toBe(true);
    call.flush(STARTED, { status: 201, statusText: 'Created' });
    await launching;

    expect(ref.close).toHaveBeenCalledWith(STARTED);
  });

  it('goes back to the step at fault of a refusal', async () => {
    const { store, api, ref } = await ready({
      ...HEADER,
      origin: 'relaunch',
      versions: ['16.18.1'],
    });

    const launching = store.launch();
    (await sent(api, PASSES, 'POST')).flush(
      { code: 'validation-failed', errors: { locales: ['malformed'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await launching;

    expect(store.step()).toBe(2);
    expect(store.errors()?.fields.locales).toEqual([
      { key: 'prerender.launcher.errors.locales.malformed' },
    ]);
    expect(ref.close).not.toHaveBeenCalled();

    store.update({ locales: ['fr'] });
    expect(store.errors()).toBeNull();
  });

  it('locks the launch once another pass is active', async () => {
    const { store, api } = await ready({ ...HEADER, origin: 'relaunch', versions: ['16.18.1'] });

    const launching = store.launch();
    (await sent(api, PASSES, 'POST')).flush(
      { code: 'prerender-pass-active' },
      { status: 409, statusText: 'Conflict' },
    );
    await launching;

    expect(store.errors()?.band).toBe('active');
    expect(store.canLaunch()).toBe(false);
  });

  it('checks after no answer: a newer pass closes the dialog', async () => {
    const { store, api, ref } = await ready({
      ...HEADER,
      origin: 'relaunch',
      versions: ['16.18.1'],
    });

    await store.launch();
    expect(store.errors()?.band).toBe('network');

    const checking = store.check();
    const newer = { ...RUNNING, id: 4, units: { ...RUNNING.units, total: 16 } };
    (await sent(api, REPORT_PATH)).flush(prerenderReport({ current: newer }));
    await checking;

    expect(ref.close).toHaveBeenCalledWith({
      id: 4,
      epoch: 2,
      units: 16,
      skippedUnits: 0,
      ignoredVersions: [],
    });
  });

  it('checks after no answer: no newer pass launches again', async () => {
    const { store, api, ref } = await ready({
      ...HEADER,
      origin: 'relaunch',
      versions: ['16.18.1'],
    });

    await store.launch();

    const checking = store.check();
    (await sent(api, REPORT_PATH)).flush(prerenderReport());
    await checking;

    expect(ref.close).not.toHaveBeenCalled();
    expect(store.errors()).toBeNull();
    expect(store.canLaunch()).toBe(true);
  });
});
