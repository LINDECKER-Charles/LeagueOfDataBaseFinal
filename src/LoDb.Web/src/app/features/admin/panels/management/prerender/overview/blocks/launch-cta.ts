import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { Button } from '../../../../../../../ui/controls/button';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { LiveBadge } from '../../live/badge/live-badge';
import type { LivePoll } from '../../live/poll/inject-live-poll';
import type { LaunchLock } from '../state/launch-lock-of';

const REASON_ID = 'prerender-launch-lock';

/**
 * The actions of the page's head (§ 2.1): how fresh the report is, « Actualiser », then
 * « Lancer une passe », the one strong call of the page. While no pass may start the button
 * is disabled and says why under it, never in a tooltip, with a way to the pass that holds
 * it. On a phone the launch takes the whole width.
 */
@Component({
  selector: 'lodb-launch-cta',
  imports: [AdminTextPipe, Button, LiveBadge],
  template: `
    <div class="flex flex-wrap items-center gap-3 max-sm:flex-col max-sm:items-stretch">
      <lodb-live-badge
        class="max-sm:order-3"
        [poll]="poll()"
        [every]="every()"
        [isCompact]="true"
      />
      <button
        class="max-sm:order-2"
        lodbButton="ghost"
        type="button"
        data-refresh
        [attr.aria-busy]="poll().busy()"
        (click)="refresh.emit()"
      >
        {{ 'admin.prerender.overview.cta.refresh' | adminText }}
      </button>
      <button
        class="max-sm:order-1"
        lodbButton="gold"
        type="button"
        data-launch
        [disabled]="lock() !== null"
        [attr.aria-describedby]="lock() ? reasonId : null"
        (click)="launch.emit()"
      >
        {{ 'admin.prerender.overview.cta.launch' | adminText }}
      </button>
    </div>
    @if (lock(); as held) {
      <p class="m-0 text-[0.8rem] text-text-dim sm:text-end" [id]="reasonId" data-launch-lock>
        {{ 'admin.prerender.overview.cta.locked_' + held.kind | adminText: held }}
        @if (held.kind === 'active') {
          <button class="ms-1 text-hex underline" type="button" (click)="follow.emit()">
            {{ 'admin.prerender.overview.cta.follow' | adminText }}
          </button>
        }
      </p>
    }
  `,
  host: { class: 'flex flex-col gap-2 max-sm:w-full' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LaunchCta {
  /** Why no pass may start, null when one may. */
  readonly lock = input.required<LaunchLock | null>();
  /** The report's reads, whose freshness the head shows. */
  readonly poll = input.required<LivePoll<PrerenderReport>>();
  /** The report's cadence now, in milliseconds. */
  readonly every = input.required<number>();
  readonly launch = output<void>();
  readonly refresh = output<void>();
  /** The way to the monitor of the pass that holds the launch. */
  readonly follow = output<void>();

  protected readonly reasonId = REASON_ID;
}
