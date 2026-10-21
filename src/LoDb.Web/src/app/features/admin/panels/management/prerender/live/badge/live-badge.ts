import { ChangeDetectionStrategy, Component, computed, input, untracked } from '@angular/core';
import { Button } from '../../../../../../../ui/controls/button';
import { duration } from '../../../../../format/duration';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import type { LivePoll } from '../poll/inject-live-poll';
import { type Freshness, freshnessOf } from './freshness-of';
import { injectNow } from './inject-now';
import { relativeTime } from './relative-time';

const LIVE = 'prerender.common.live';
const SECOND_MS = 1_000;
const NO_RESPONSE = 0;

/**
 * How fresh a live read is (§ 2.5): a dot and a line, « En direct · mis à jour il y a 4 s »,
 * « Mise à jour en retard », « Hors ligne depuis 2 min (HTTP 502) · nouvel essai dans 18 s »
 * with a way to try again at once, or « En pause tant que l'onglet est masqué ». The line
 * moves every second; only a change of state is announced. Compact, for the head of the
 * page: the dot and « il y a 4 s », the whole line in its title.
 */
@Component({
  selector: 'lodb-live-badge',
  imports: [AdminTextPipe, Button],
  template: `
    @if (freshness(); as state) {
      <span class="live-dot" [attr.data-freshness]="state" aria-hidden="true"></span>
      @if (isCompact()) {
        <span class="text-text-dim" [title]="line()" data-live-line>{{ ago() }}</span>
      } @else {
        <span class="text-text-muted" data-live-line>{{ line() }}</span>
        @if (state === 'lost') {
          <button lodbButton="ghost" lodbButtonSize="small" type="button" (click)="poll().reload()">
            {{ 'admin.prerender.common.live.retry' | adminText }}
          </button>
        }
      }
    }
    <span class="sr-only" aria-live="polite">{{ announced() }}</span>
  `,
  styles: `
    .live-dot {
      inline-size: 0.5rem;
      block-size: 0.5rem;
      flex: none;
      border-radius: 50%;
      background: var(--color-text-dim);
    }
    .live-dot[data-freshness='live'] {
      background: var(--color-good);
      animation: live-pulse 2s ease-in-out infinite;
    }
    .live-dot[data-freshness='late'] {
      background: var(--color-gold);
    }
    .live-dot[data-freshness='lost'] {
      background: var(--color-bad);
    }
    @keyframes live-pulse {
      50% {
        box-shadow: 0 0 0 0.3rem color-mix(in srgb, var(--color-good) 25%, transparent);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .live-dot[data-freshness='live'] {
        animation: none;
      }
    }
  `,
  host: { class: 'inline-flex flex-wrap items-center gap-2 text-[0.78rem]' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LiveBadge {
  readonly poll = input.required<LivePoll<unknown>>();
  readonly every = input.required<number>();
  /** Compact: the dot and « il y a 4 s » only, for the page header (§ 2.4). */
  readonly isCompact = input(false);

  private readonly texts = injectAdminText();
  private readonly now = injectNow(SECOND_MS);

  protected readonly freshness = computed(() => {
    const poll = this.poll();
    const read = { lastSuccessAt: poll.lastSuccessAt(), every: this.every() };
    return freshnessOf({ ...read, isHidden: poll.isHidden() }, this.now());
  });
  protected readonly ago = computed(() => {
    const at = this.poll().lastSuccessAt();
    return at === null ? '' : relativeTime(at, this.now(), this.texts);
  });
  protected readonly line = computed(() => {
    const state = this.freshness();
    return state ? this.lineOf(state) : '';
  });
  // Read again only when the state changes: a screen reader hears a change, not each second.
  protected readonly announced = computed(() => {
    this.freshness();
    return untracked(() => this.line());
  });

  private lineOf(state: Freshness): string {
    if (state === 'live' || state === 'late') {
      return this.texts.text(`${LIVE}.${state}`, { ago: this.ago() });
    }
    if (state === 'hidden') {
      return this.texts.text(`${LIVE}.hidden`);
    }
    const poll = this.poll();
    const next = poll.nextAttemptAt();
    return this.texts.text(`${LIVE}.lost`, {
      since: duration((this.now() - (poll.lastSuccessAt() ?? this.now())) / SECOND_MS),
      reason: this.reason(),
      next: next === null ? '—' : relativeTime(next, this.now(), this.texts),
    });
  }

  // Why the reads fail, as the panels say it: « HTTP 502 », « API injoignable ».
  private reason(): string {
    const failure = this.poll().failure() ?? { kind: 'network', status: NO_RESPONSE };
    return this.texts.text(`state.reasons.${failure.kind}`, { status: failure.status });
  }
}
