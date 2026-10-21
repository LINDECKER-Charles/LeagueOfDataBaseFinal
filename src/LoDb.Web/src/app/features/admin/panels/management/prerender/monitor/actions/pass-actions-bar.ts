import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import { Button } from '../../../../../../../ui/controls/button';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';

const RUNNING = 'running';
const PAUSED = 'paused';

/**
 * The moves of the active pass (§ 4.7): « Mettre en pause » and « Annuler la passe… » while
 * it runs; « Reprendre » (« Reprendre… » when it paused itself, as a question follows) and
 * « Annuler la passe… » while paused. It only asks: the monitor says the consequences, then
 * acts. Every button waits while a move is in flight.
 */
@Component({
  selector: 'lodb-pass-actions-bar',
  imports: [AdminTextPipe, Button],
  template: `
    @if (isRunning()) {
      <button
        lodbButton="ghost"
        lodbButtonSize="small"
        type="button"
        data-action="pause"
        [disabled]="busy()"
        (click)="paused.emit()"
      >
        {{ 'admin.prerender.live.actions.pause' | adminText }}
      </button>
    }
    @if (isPaused()) {
      <button
        lodbButton="gold"
        lodbButtonSize="small"
        type="button"
        data-action="resume"
        [disabled]="busy()"
        (click)="resumed.emit()"
      >
        {{ resumeKey() | adminText }}
      </button>
    }
    @if (isRunning() || isPaused()) {
      <button
        lodbButton="danger"
        lodbButtonSize="small"
        type="button"
        data-action="cancel"
        [disabled]="busy()"
        (click)="cancelled.emit()"
      >
        {{ 'admin.prerender.live.actions.cancel' | adminText }}
      </button>
    }
  `,
  host: { class: 'flex flex-wrap items-center justify-end gap-3' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PassActionsBar {
  readonly pass = input.required<PrerenderPassView>();
  /** Whether a move is in flight. */
  readonly busy = input(false);
  readonly paused = output<void>();
  readonly resumed = output<void>();
  readonly cancelled = output<void>();

  protected readonly isRunning = computed(() => this.pass().state === RUNNING);
  protected readonly isPaused = computed(() => this.pass().state === PAUSED);
  protected readonly resumeKey = computed(() =>
    this.pass().pauseReason
      ? 'admin.prerender.live.actions.resume_itself'
      : 'admin.prerender.live.actions.resume',
  );
}
