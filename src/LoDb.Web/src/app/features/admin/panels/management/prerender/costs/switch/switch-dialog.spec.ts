import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderEpochCoverage } from '../../../../../../../core/api/generated/models/prerender-epoch-coverage';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { SwitchDialog, type SwitchData } from './switch-dialog';

const BASE = prerenderReport();
const COVERAGE = BASE.storage.epochs[0].coverage as PrerenderEpochCoverage;

function withStandby(coverage: PrerenderEpochCoverage, changes: Partial<PrerenderReport> = {}) {
  const standby = { ...BASE.storage.epochs[0], epoch: 3, state: 'standby', isWriting: false };
  return prerenderReport({
    current: null,
    storage: { ...BASE.storage, epochs: [{ ...standby, coverage }, ...BASE.storage.epochs] },
    ...changes,
  });
}

function open(data: SwitchData): { fixture: ComponentFixture<SwitchDialog>; close: () => void } {
  const close = vi.fn();
  configureAdminTestBed(
    [],
    [
      { provide: DIALOG_DATA, useValue: data },
      { provide: DialogRef, useValue: { close } },
    ],
  );
  const fixture = TestBed.createComponent(SwitchDialog);
  fixture.detectChanges();
  return { fixture, close };
}

function words(fixture: ComponentFixture<SwitchDialog>, selector: string): string[] {
  return [...(fixture.nativeElement as HTMLElement).querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('SwitchDialog', () => {
  it('gives the variable to set, and leaves the retirement in words', () => {
    const whole = { ...COVERAGE, gapUnits: 0, completeUnits: 8_430 };
    const { fixture } = open({ target: 3, report: withStandby(whole) });
    const page = fixture.nativeElement as HTMLElement;

    expect(words(fixture, '[data-variable]')).toEqual(['LODB_ARCHIVE_SERVE_EPOCH=3']);
    expect(page.querySelectorAll('lodb-copy-link')).toHaveLength(1);
    expect(page.textContent).not.toContain('RETIRED_EPOCHS');
    expect(words(fixture, '[data-step-retire]')).toEqual(['admin.prerender.switch.step_retire']);
    expect(page.classList).toContain('admin-overlay');
  });

  it('says it is ready, with what the SSR keeps serving', () => {
    const whole = { ...COVERAGE, gapUnits: 0, completeUnits: 8_430 };
    const { fixture } = open({ target: 3, report: withStandby(whole) });

    expect(words(fixture, '[data-switch-state]')).toEqual([
      '✓ admin.prerender.switch.ready',
      'admin.prerender.switch.ready_ssr',
    ]);
  });

  it('says how much an epoch in part leaves to the SSR', () => {
    const partial = { ...COVERAGE, completeUnits: 5_420, gapUnits: 3_010 };
    const { fixture } = open({ target: 3, report: withStandby(partial) });

    expect(words(fixture, '[data-switch-state]')).toEqual(['admin.prerender.switch.partial']);
  });

  it('warns that a pass still writes the epoch', () => {
    const { fixture } = open({ target: 2, report: BASE });

    expect(words(fixture, '[data-writing]')).toEqual(['admin.prerender.switch.writing']);
  });

  it('closes', () => {
    const { fixture, close } = open({ target: 2, report: BASE });
    const buttons = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];

    buttons
      .find((button) => button.textContent?.trim() === 'admin.prerender.switch.close')
      ?.click();

    expect(close).toHaveBeenCalled();
  });
});
