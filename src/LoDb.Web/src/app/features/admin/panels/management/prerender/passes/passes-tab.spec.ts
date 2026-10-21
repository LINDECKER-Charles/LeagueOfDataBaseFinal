import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../../core/api/generated/models/prerender-report';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import { prerenderReport } from '../../../../testing/fixtures/prerender-report';
import { PassesTab } from './passes-tab';

const [RUNNING, CANCELLED] = prerenderReport().passes;

function open(report: PrerenderReport = prerenderReport()) {
  configureAdminTestBed();
  const fixture = TestBed.createComponent(PassesTab);
  fixture.componentRef.setInput('report', report);
  const details: number[] = [];
  let follows = 0;
  fixture.componentInstance.detail.subscribe((id) => details.push(id));
  fixture.componentInstance.follow.subscribe(() => follows++);
  fixture.detectChanges();
  return { fixture, details, follows: () => follows };
}

function page(fixture: ComponentFixture<PassesTab>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function cells(fixture: ComponentFixture<PassesTab>, id: number): string[] {
  return [...page(fixture).querySelectorAll(`tr[data-pass="${id}"] :is(th, td)`)].map((cell) =>
    (cell.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('PassesTab', () => {
  it('lists the passes, each with its epoch, state, perimeter, progress and dates', () => {
    const { fixture } = open();

    expect(cells(fixture, 2)).toEqual([
      '#2',
      '1',
      'admin.prerender.states.cancelled',
      // Without a catalogue, a type of page reads as its own name.
      'admin.prerender.common.scope.range · fr · items',
      'admin.prerender.passes.pages',
      '240.00 MB',
      '1 h 0 min',
      'admin.prerender.passes.automatic',
      '15/10/2026 08:00',
      '15/10/2026 09:00',
    ]);
    expect(page(fixture).querySelector('tr[data-pass="2"] lodb-stacked-bar')).not.toBeNull();
  });

  it('puts the active pass first, with a way back to its monitor', () => {
    const report = prerenderReport({ passes: [CANCELLED, RUNNING] });
    const { fixture, follows } = open(report);
    const rows = [...page(fixture).querySelectorAll('tr[data-pass]')];

    expect(rows.map((row) => row.getAttribute('data-pass'))).toEqual(['3', '2']);
    expect(page(fixture).querySelectorAll('[data-follow]')).toHaveLength(1);
    page(fixture).querySelector<HTMLButtonElement>('[data-follow]')?.click();
    expect(follows()).toBe(1);
  });

  it('opens the detail of a pass from anywhere in its row, not from « Suivre »', () => {
    const { fixture, details } = open();

    page(fixture).querySelector<HTMLElement>('tr[data-pass="2"] td')?.click();
    page(fixture).querySelector<HTMLButtonElement>('[data-follow]')?.click();

    expect(details).toEqual([2]);
  });

  it('opens the detail of a pass by its number', () => {
    const { fixture, details } = open();

    page(fixture).querySelector<HTMLButtonElement>('tr[data-pass="2"] th button')?.click();

    expect(details).toEqual([2]);
  });

  it('folds the passes past ten', () => {
    const passes: PrerenderPassView[] = Array.from({ length: 12 }, (_, index) => ({
      ...CANCELLED,
      id: 20 - index,
    }));
    const { fixture } = open(prerenderReport({ current: null, passes }));

    expect(page(fixture).querySelectorAll('tr[data-pass]')).toHaveLength(10);
    expect(page(fixture).querySelector('lodb-fold-toggle button')).not.toBeNull();
  });

  it('says when no pass was ever launched', () => {
    const { fixture } = open(prerenderReport({ current: null, passes: [] }));

    expect(page(fixture).textContent).toContain('admin.prerender.passes.empty');
  });
});
