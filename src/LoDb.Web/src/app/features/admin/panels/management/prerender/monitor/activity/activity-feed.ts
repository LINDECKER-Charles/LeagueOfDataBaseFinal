import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  signal,
  untracked,
} from '@angular/core';
import { duration } from '../../../../../format/duration';
import { stamp } from '../../../../../format/stamp';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import type { ActivityEvent, ActivityKind } from './activity-events';

/** A line of the feed, written. */
interface FeedLine {
  readonly key: string;
  readonly kind: ActivityKind;
  readonly time: string;
  readonly icon: string;
  readonly text: string;
  readonly reason: string | null;
}

const FEED = 'prerender.live.feed';
const SHOWN = 8;
// The day of a stamp, « 21/10/2026 », and its hour, « 08:42 ».
const DAY_LENGTH = 10;
const HOUR_LENGTH = 5;
const ANNOUNCE_GAP_MS = 10_000;
const ICONS: Readonly<Record<ActivityKind, string>> = {
  done: '✓',
  done_failures: '⚠',
  failed: '✕',
  joined: '⊕',
  total_up: '↗',
  total_up_joined: '↗',
  paused: '⏸',
  paused_itself: '⏸',
  resumed: '▶',
  completed: '■',
  cancelled: '✕',
};
// What a screen reader hears of the feed: a unit that failed, a pause of its own, the end.
const ANNOUNCED: readonly ActivityKind[] = ['failed', 'paused_itself', 'completed', 'cancelled'];

/**
 * The activity of the pass (§ 4.6): units ended, versions joined, totals re-estimated, moves
 * of the pass, newest first; eight shown, then « Tout afficher ». The reasons are the
 * pass's own English, in monospace, cut to a line and whole on hover. The feed itself is
 * not live: a separate polite region says only a unit failed, a pause of its own and the
 * end, once every 10 seconds at most.
 */
@Component({
  selector: 'lodb-activity-feed',
  imports: [AdminTextPipe],
  templateUrl: './activity-feed.html',
  host: { class: 'flex min-w-0 flex-col gap-2' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActivityFeed {
  readonly events = input.required<readonly ActivityEvent[]>();
  /** The server's clock, in milliseconds: a line of today shows its hour alone. */
  readonly serverNow = input.required<number>();

  private readonly texts = injectAdminText();
  private announcedAt = 0;
  // Null until the first lines: what the page finds on opening is not news.
  private announcedKey: string | null = null;

  protected readonly isOpen = signal(false);
  protected readonly lines = computed(() => {
    const today = stamp(new Date(this.serverNow()).toISOString(), 'minute').slice(0, DAY_LENGTH);
    const events = this.isOpen() ? this.events() : this.events().slice(0, SHOWN);
    return events.map((event) => this.lineOf(event, today));
  });
  protected readonly hidden = computed(() => Math.max(0, this.events().length - SHOWN));
  protected readonly announced = signal('');

  constructor() {
    effect(() => {
      const event = this.events().find((candidate) => ANNOUNCED.includes(candidate.kind));
      untracked(() => this.announce(event));
    });
  }

  private announce(event: ActivityEvent | undefined): void {
    const now = Date.now();
    const key = event?.key ?? '';
    if (this.announcedKey === null || this.events().length === 0) {
      this.announcedKey = this.events().length === 0 ? null : key;
      return;
    }
    if (!event || key === this.announcedKey || now - this.announcedAt < ANNOUNCE_GAP_MS) {
      return;
    }
    this.announcedKey = key;
    this.announcedAt = now;
    this.announced.set(this.textOf(event));
  }

  private lineOf(event: ActivityEvent, today: string): FeedLine {
    const written = stamp(event.at, 'minute');
    return {
      key: event.key,
      kind: event.kind,
      time: written.startsWith(today) ? written.slice(-HOUR_LENGTH) : written,
      icon: ICONS[event.kind],
      text: this.textOf(event),
      reason: event.reason ?? null,
    };
  }

  private textOf(event: ActivityEvent): string {
    const { version, locale, pages, failed, units, seconds } = event;
    return this.texts.text(`${FEED}.${event.kind}`, {
      version,
      locale,
      pages,
      failed,
      units,
      duration: duration(seconds ?? 0),
    });
  }
}
