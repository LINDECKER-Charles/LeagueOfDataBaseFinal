import {
  ChangeDetectionStrategy,
  Component,
  afterRenderEffect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LoadClock } from './load-clock';
import type { LoadTimes } from './load-times';
import { loadTimesOf } from './load-times-of';

/**
 * A discreet badge of the time the page took: the server's share, then the client's. Measured
 * once the page has rendered, in the browser only: the server renders nothing, so hydration
 * finds nothing to match. Never in the way of a click beneath it.
 */
@Component({
  selector: 'lodb-load-time',
  imports: [TranslocoPipe],
  template: `@if (times(); as shown) {
    <div class="perf" role="status" [attr.aria-label]="'perf.title' | transloco">
      <span class="perf__spark" aria-hidden="true"></span>
      <span class="perf__pair">
        <span class="perf__key">{{ 'perf.server' | transloco }}</span>
        <span class="perf__value">{{ format(shown.serverMs) }}</span>
      </span>
      <span class="perf__sep" aria-hidden="true">·</span>
      <span class="perf__pair">
        <span class="perf__key">{{ 'perf.client' | transloco }}</span>
        <span class="perf__value">{{ format(shown.clientMs) }}</span>
      </span>
    </div>
  }`,
  styleUrl: './load-time.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoadTime {
  private readonly clock = inject(LoadClock);
  protected readonly times = signal<LoadTimes | null>(null);

  constructor() {
    afterRenderEffect(() => {
      const record = this.clock.last();
      if (record !== null) {
        untracked(() => this.times.set(loadTimesOf(record, performance)));
      }
    });
  }

  protected format(ms: number | null): string {
    return ms === null ? '—' : `${Math.round(ms)} ms`;
  }
}
