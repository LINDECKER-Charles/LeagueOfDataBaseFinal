import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { RetireDialog } from './retire-dialog';
import type { SwitchData } from './switch-dialog';

function open(data: SwitchData): ComponentFixture<RetireDialog> {
  configureAdminTestBed(
    [],
    [
      { provide: DIALOG_DATA, useValue: data },
      { provide: DialogRef, useValue: { close: vi.fn() } },
    ],
  );
  const fixture = TestBed.createComponent(RetireDialog);
  fixture.detectChanges();
  return fixture;
}

function page(fixture: ComponentFixture<RetireDialog>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

describe('RetireDialog', () => {
  it('gives the variables of the retirement, copiable, once the epoch is retired', () => {
    const fixture = open({ target: 1, report: prerenderReport() });

    expect(page(fixture).querySelector('[data-retire-line]')?.textContent?.trim()).toBe(
      'ENVIRONMENT=prod BUCKET=lodb-pages-prod SERVED_EPOCH=2 WRITE_EPOCH=2 RETIRED_EPOCHS=1',
    );
    expect(page(fixture).querySelectorAll('lodb-copy-link')).toHaveLength(1);
    expect(page(fixture).querySelector('[data-no-bucket]')).toBeNull();
    expect(page(fixture).classList).toContain('admin-overlay');
  });

  it('keeps a placeholder, not to be copied, without the bucket', () => {
    const fixture = open({ target: 1, report: prerenderReport({ storeBucket: null }) });

    expect(page(fixture).querySelector('[data-retire-line]')?.textContent).toContain(
      'BUCKET=<bucket>',
    );
    expect(page(fixture).querySelector('lodb-copy-link')).toBeNull();
    expect(page(fixture).querySelector('[data-no-bucket]')).not.toBeNull();
  });

  it('gives no command for an epoch still served or newer', () => {
    const fixture = open({ target: 2, report: prerenderReport() });

    expect(page(fixture).querySelector('[data-retire-line]')).toBeNull();
    expect(page(fixture).querySelector('lodb-copy-link')).toBeNull();
  });
});
