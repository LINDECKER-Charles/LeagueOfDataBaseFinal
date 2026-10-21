import { ChangeDetectionStrategy, Component, computed, input, untracked } from '@angular/core';
import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';
import { duration } from '../../../../../format/duration';
import { figure } from '../../../../../format/figure';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import type { FailingUnit } from '../now/failing-unit-of';
import type { Stall } from '../now/stall-of';
import { type Alarm, alarmOf } from './alarm-of';

const LIVE = 'prerender.live';

/**
 * The one status of the monitor (§ 4.5), for a screen reader: always there, its text alone
 * changing, since a band inserted with its text as `role="status"` is often not read (NVDA,
 * JAWS). It says what goes wrong once, as it starts (`alarmOf`): a unit failing in numbers
 * and its reason, a stall and its cause, the background task off, the SSR out of reach.
 */
@Component({
  selector: 'lodb-monitor-status',
  template: `<p class="sr-only" role="status" data-monitor-status>{{ announced() }}</p>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MonitorStatus {
  readonly progress = input.required<PrerenderPassProgress | undefined>();
  readonly stall = input.required<Stall | null>();
  readonly failing = input.required<FailingUnit | null>();

  private readonly texts = injectAdminText();
  // Equal while the same thing goes wrong: its counts and durations move, it does not.
  private readonly alarm = computed(() => alarmOf(this.progress(), this.stall(), this.failing()), {
    equal: (one, other) => JSON.stringify(one) === JSON.stringify(other),
  });

  protected readonly announced = computed(() => {
    const alarm = this.alarm();
    return untracked(() => (alarm ? this.textOf(alarm) : ''));
  });

  private textOf(alarm: Alarm): string {
    switch (alarm.kind) {
      case 'failing':
        return this.failingText();
      case 'stalled':
        return `${this.stallTitle()} · ${this.texts.text(`${LIVE}.stall.${alarm.cause}`)}`;
      case 'ssr':
        return this.texts.text(`${LIVE}.stall.ssr`);
      default:
        return this.texts.text(`${LIVE}.now.worker_off`);
    }
  }

  // The unit, its failed pages and its last reason, in the pass's English.
  private failingText(): string {
    const failing = this.failing();
    if (!failing) {
      return '';
    }
    const { unit, processed, share } = failing;
    const line = this.texts.text(`${LIVE}.failing`, {
      ...unit,
      failed: unit.pagesFailed,
      processed: figure(processed),
      share: figure(share, 'percent'),
    });
    return `${line} ${unit.failure ?? ''}`.trim();
  }

  private stallTitle(): string {
    const stall = this.stall();
    const idle = stall?.kind === 'stalled' ? stall.idleSeconds : 0;
    return this.texts.text(`${LIVE}.stall.title`, { duration: duration(idle) });
  }
}
