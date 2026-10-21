import { HttpTestingController } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { prerenderProgress } from '../../../../../testing/fixtures/prerender-progress';
import { sent } from '../../../../../testing/http/sent';
import type { LaunchPreset } from '../../common/scope/launch-preset';
import { PassRecap } from './pass-recap';

const PROGRESS = '/api/admin/prerender/passes/12/progress';
const DISMISSED_KEY = 'lodb.admin.prerender.recap-dismissed';
const ENDED = {
  ...prerenderProgress().pass,
  finishedAt: '2026-10-23T14:00:00+00:00',
  pagesLeft: 0,
  units: { total: 4_452, pending: 0, running: 0, completed: 4_435, failed: 17 },
};
const COMPLETED: PrerenderPassView = { ...ENDED, state: 'completed' };
const CANCELLED: PrerenderPassView = { ...ENDED, state: 'cancelled' };
// The whole progress of the pass: 16.18.1 lost pages in de, 15.2.1 failed in th.
const VERSIONS = [
  { ...prerenderProgress().versions[0], cells: 'ccw' + 'c'.repeat(18), pagesTotal: 21_000 },
  {
    ...prerenderProgress().versions[0],
    version: '15.2.1',
    rank: 2,
    cells: 'c'.repeat(16) + 'f' + 'c'.repeat(4),
    pagesTotal: 10_500,
  },
];

@Component({
  imports: [PassRecap],
  template: `
    <lodb-pass-recap
      [pass]="pass()"
      [servedEpoch]="2"
      (launch)="presets.push($event)"
      (detail)="details.push($event)"
      (dismissed)="dismissals.push($event)"
    />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  readonly pass = signal(COMPLETED);
  readonly presets: LaunchPreset[] = [];
  readonly details: number[] = [];
  readonly dismissals: number[] = [];
}

function open(pass: PrerenderPassView): ComponentFixture<Host> {
  configureAdminTestBed();
  const fixture = TestBed.createComponent(Host);
  fixture.componentInstance.pass.set(pass);
  fixture.detectChanges();
  return fixture;
}

async function settle(fixture: ComponentFixture<Host>): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
}

function button(fixture: ComponentFixture<Host>, selector: string): HTMLButtonElement {
  const found = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(selector);
  if (!found) {
    throw new Error(`No button matches ${selector}.`);
  }
  return found;
}

function buttonReading(fixture: ComponentFixture<Host>, label: string): HTMLButtonElement {
  const buttons = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];
  const found = buttons.find((candidate) => candidate.textContent?.trim() === label);
  if (!found) {
    throw new Error(`No button reads "${label}".`);
  }
  return found;
}

describe('PassRecap', () => {
  beforeEach(() => localStorage.removeItem(DISMISSED_KEY));

  it('sums up a completed pass, and relaunches what failed with its figures', async () => {
    const fixture = open(COMPLETED);
    const http = { http: TestBed.inject(HttpTestingController) };
    const read = await sent(http, PROGRESS);
    expect(read.request.params.get('since')).toBeNull();
    read.flush(prerenderProgress({ isComplete: true, pass: COMPLETED, versions: VERSIONS }));
    await settle(fixture);

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('h2')?.textContent?.trim()).toBe(
      'admin.prerender.live.recap.completed',
    );
    expect(page.querySelector('[data-recap-summary]')?.textContent?.trim()).toBe(
      'admin.prerender.live.recap.summary',
    );
    button(fixture, '[data-relaunch]').click();
    expect(button(fixture, '[data-relaunch]').textContent?.trim()).toBe(
      'admin.prerender.common.retry.units',
    );
    expect(fixture.componentInstance.presets[0]).toMatchObject({
      origin: 'failures',
      versions: ['16.18.1', '15.2.1'],
      epoch: 2,
      fromPass: 12,
    });
  });

  it('offers no relaunch when nothing is to render again', async () => {
    const fixture = open(COMPLETED);
    const http = { http: TestBed.inject(HttpTestingController) };
    const clean = [{ ...VERSIONS[0], cells: 'c'.repeat(21) }];
    (await sent(http, PROGRESS)).flush(
      prerenderProgress({ isComplete: true, pass: COMPLETED, versions: clean }),
    );
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-relaunch]')).toBeNull();
  });

  it('finishes a cancelled pass first, then offers to complete its epoch', async () => {
    const fixture = open(CANCELLED);
    const http = { http: TestBed.inject(HttpTestingController) };
    // Cancelled with 16.20.1 cut short in fr and never reached past it.
    const cut = [
      { ...VERSIONS[0], version: '16.20.1', cells: 'c'.repeat(6) + 'i' + '.'.repeat(14) },
    ];
    (await sent(http, PROGRESS)).flush(
      prerenderProgress({ isComplete: true, pass: CANCELLED, versions: cut }),
    );
    await settle(fixture);

    expect(button(fixture, '[data-relaunch]').textContent?.trim()).toBe(
      'admin.prerender.live.recap.finish',
    );
    button(fixture, '[data-relaunch]').click();
    expect(fixture.componentInstance.presets[0]).toMatchObject({
      origin: 'failures',
      versions: ['16.20.1'],
      skipComplete: true,
      fromPass: 12,
    });
    // The epoch's other gaps come second: a ghost, not the primary action.
    expect(button(fixture, '[data-complete]').classList).toContain('hx-btn-ghost');
    expect(button(fixture, '[data-relaunch]').classList).not.toContain('hx-btn-ghost');
  });

  it('completes the epoch of a cancelled pass that left nothing undone', async () => {
    const fixture = open(CANCELLED);
    const http = { http: TestBed.inject(HttpTestingController) };
    const clean = [{ ...VERSIONS[0], cells: 'c'.repeat(21) }];
    (await sent(http, PROGRESS)).flush(
      prerenderProgress({ isComplete: true, pass: CANCELLED, versions: clean }),
    );
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-relaunch]')).toBeNull();
    button(fixture, '[data-complete]').click();
    expect(fixture.componentInstance.presets).toEqual([
      {
        origin: 'complete-epoch',
        versions: 'incomplete',
        priority: [],
        locales: null,
        pageTypes: null,
        epoch: 2,
        skipComplete: true,
        fromPass: null,
      },
    ]);
  });

  it('opens the detail of the pass', async () => {
    const fixture = open(CANCELLED);
    await settle(fixture);

    buttonReading(fixture, 'admin.prerender.live.recap.detail').click();
    expect(fixture.componentInstance.details).toEqual([12]);
  });

  it('remembers a dismissal in this browser', async () => {
    const fixture = open(CANCELLED);
    await settle(fixture);

    buttonReading(fixture, 'admin.prerender.live.recap.dismiss').click();
    expect(fixture.componentInstance.dismissals).toEqual([12]);
    expect(localStorage.getItem(DISMISSED_KEY)).toBe('12');
  });
});
