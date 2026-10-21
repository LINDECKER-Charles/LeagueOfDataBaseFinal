import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Button } from '../../../../../../../ui/controls/button';
import { figure } from '../../../../../format/figure';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import type { FailingUnit } from './failing-unit-of';

const LIVE = 'prerender.live';

/**
 * The band of a unit failing in numbers (§ 4.5), red, at the head of the monitor: which unit,
 * how many of its pages failed and its last reason, in the pass's English; then its version's
 * detail and the pause, before the rest of the pass fails as well.
 */
@Component({
  selector: 'lodb-failing-band',
  imports: [AdminTextPipe, Button],
  template: `
    <div class="min-w-0 flex-1">
      <p class="m-0" data-failing-line>{{ line() }}</p>
      @if (failing().unit.failure; as reason) {
        <p class="m-0 mt-1 font-mono text-[0.8rem]" dir="ltr" data-failing-reason>
          {{ reason }}
        </p>
      }
    </div>
    <button
      lodbButton="ghost"
      lodbButtonSize="small"
      type="button"
      (click)="opened.emit(failing().unit.version)"
    >
      {{ 'admin.prerender.live.now.detail' | adminText }}
    </button>
    <button
      lodbButton="gold"
      lodbButtonSize="small"
      type="button"
      data-failing-pause
      [disabled]="busy()"
      (click)="paused.emit()"
    >
      {{ 'admin.prerender.live.actions.pause' | adminText }}
    </button>
  `,
  host: { class: 'monitor-band monitor-band--bad', 'data-failing': '' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FailingBand {
  readonly failing = input.required<FailingUnit>();
  /** Whether a move of the pass is in flight. */
  readonly busy = input(false);
  /** The version's detail, asked for. */
  readonly opened = output<string>();
  readonly paused = output<void>();

  private readonly texts = injectAdminText();

  protected readonly line = computed(() => {
    const { unit, processed, share } = this.failing();
    return this.texts.text(`${LIVE}.failing`, {
      version: unit.version,
      locale: unit.locale,
      failed: unit.pagesFailed,
      processed: figure(processed),
      share: figure(share, 'percent'),
    });
  });
}
