import { HttpTestingController, type TestRequest } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderPassProgress } from '../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderReport } from '../../../../../../core/api/generated/models/prerender-report';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import { prerenderProgress } from '../../../../testing/fixtures/prerender-progress';
import { prerenderReport } from '../../../../testing/fixtures/prerender-report';
import { sent } from '../../../../testing/http/sent';
import type { LaunchPreset } from '../common/scope/launch-preset';
import { PRERENDER_CADENCE } from '../common/scope/prerender-cadence';
import { LiveMonitor } from './live-monitor';

const PROGRESS = '/api/admin/prerender/passes/12/progress';
// Read again within a few ticks: a spec answers each read it waits for.
const CADENCE_MS = 30;
const CADENCE = {
  progressRunningMs: CADENCE_MS,
  progressPausedMs: CADENCE_MS,
  reportActiveMs: CADENCE_MS,
  reportIdleMs: CADENCE_MS,
  reportHiddenMs: CADENCE_MS,
  versionMs: CADENCE_MS,
};
const UNTIL_MS = 2_000;
const STEP_MS = 10;

@Component({
  imports: [LiveMonitor],
  template: `
    <lodb-live-monitor
      [pass]="pass()"
      [report]="report()"
      (launch)="presets.push($event)"
      (changed)="changes = changes + 1"
      (ended)="ends.push($event)"
    />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Host {
  readonly pass = signal<PrerenderPassView>(prerenderProgress().pass);
  readonly report = signal<PrerenderReport>(prerenderReport({ current: prerenderProgress().pass }));
  readonly presets: LaunchPreset[] = [];
  readonly ends: number[] = [];
  changes = 0;
}

/**
 * The live monitor of pass 12 in a spec, the API answered by hand: each read of the progress
 * is awaited, then answered. Texts render as their keys.
 */
export class MonitorHarness {
  private constructor(
    readonly fixture: ComponentFixture<Host>,
    readonly http: HttpTestingController,
  ) {}

  /** Opens the monitor of `pass` (pass 12 running, by default). */
  static open(pass: PrerenderPassView = prerenderProgress().pass): MonitorHarness {
    configureAdminTestBed([], [{ provide: PRERENDER_CADENCE, useValue: CADENCE }]);
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.pass.set(pass);
    fixture.detectChanges();
    return new MonitorHarness(fixture, TestBed.inject(HttpTestingController));
  }

  get host(): Host {
    return this.fixture.componentInstance;
  }

  get page(): HTMLElement {
    return this.fixture.nativeElement as HTMLElement;
  }

  /** Answers the next read of the progress with `body` (complete, by default). */
  async read(body: PrerenderPassProgress = prerenderProgress({ isComplete: true })) {
    const request = await sent(this, PROGRESS);
    request.flush(body);
    await this.settle();
    return request;
  }

  /** The next read of the progress, left unanswered. */
  next(): Promise<TestRequest> {
    return sent(this, PROGRESS);
  }

  async settle(): Promise<void> {
    await this.fixture.whenStable();
    this.fixture.detectChanges();
  }

  /** The text of the first match of `selector`, its spaces folded; '' when none. */
  text(selector: string, root: ParentNode = this.page): string {
    return (root.querySelector(selector)?.textContent ?? '').replace(/\s+/g, ' ').trim();
  }

  /**
   * Waits for `check` to hold, rendering as it goes; fails past two seconds, counted in steps
   * since a spec may hold the clock still.
   */
  async until(check: () => boolean): Promise<void> {
    for (let step = 0; !check(); step++) {
      if (step * STEP_MS > UNTIL_MS) {
        throw new Error('The monitor never got there.');
      }
      await new Promise((resolve) => setTimeout(resolve, STEP_MS));
      this.fixture.detectChanges();
    }
  }

  /** The dialogs open, mounted under <body>. */
  overlay(): HTMLElement {
    const container = document.querySelector<HTMLElement>('.cdk-overlay-container');
    if (!container) {
      throw new Error('No dialog is open.');
    }
    return container;
  }
}
