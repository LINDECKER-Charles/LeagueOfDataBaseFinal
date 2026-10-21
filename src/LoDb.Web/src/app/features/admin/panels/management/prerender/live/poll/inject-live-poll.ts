import { DestroyRef, type Signal, computed, effect, inject, signal } from '@angular/core';
import { type Observable, type Subscription, first, timeout } from 'rxjs';
import { ADMIN_TIMEOUT } from '../../../../../shared/http/admin-timeout';
import type { Panel } from '../../../../../state/panel';
import { type PanelFailure, panelFailureOf } from '../../../../../state/panel-failure';
import { injectPageVisible } from './inject-page-visible';
import { pollDelay } from './poll-delay';

/** How a source is read again and again. */
export interface LivePollOptions<T> {
  /** Reads once; gets the last good value, for a delta. */
  readonly load: (last: T | undefined) => Observable<T>;
  /** Milliseconds from an answer to the next read; null stops. Read reactively. */
  readonly every: () => number | null;
  /** The same while the tab is hidden; absent or null suspends the reads until it shows. */
  readonly hiddenEvery?: () => number | null;
}

/** A source read again and again, and how fresh what it shows is. */
export interface LivePoll<T> extends Panel<T> {
  /** Date.now() at the last success. */
  readonly lastSuccessAt: Signal<number | null>;
  /** Failures in a row, none since the last success. */
  readonly failures: Signal<number>;
  /** When the next read is due (Date.now()), null while none is planned. */
  readonly nextAttemptAt: Signal<number | null>;
  readonly isHidden: Signal<boolean>;
}

const SESSION = 'session';
// A read once (`every` null) that failed before any answer tries again from this cadence,
// backing off: 10 s, 20 s, 40 s, then a minute.
const RETRY_MS = 5_000;

/** The state of the reads, and the reads themselves: one in flight at most. */
class LiveReads<T> {
  readonly value = signal<T | undefined>(undefined);
  readonly busy = signal(false);
  readonly failure = signal<PanelFailure | null>(null);
  readonly lastSuccessAt = signal<number | null>(null);
  readonly failures = signal(0);
  readonly nextAttemptAt = computed(() => this.nextAt());
  // Until the first answer, the first read is due from the opening, whatever the cadence.
  private readonly settledAt = signal<number | null>(null);
  private readonly openedAt = Date.now();
  // A 401 or a 403: the session is gone, no read succeeds until the operator signs in again.
  private readonly isStopped = signal(false);
  private isQueued = false;
  private reading: Subscription | null = null;

  constructor(
    private readonly options: LivePollOptions<T>,
    private readonly wait: number,
    readonly isHidden: Signal<boolean>,
  ) {}

  read(): void {
    if (this.busy()) {
      this.isQueued = true;
      return;
    }
    this.busy.set(true);
    this.reading = this.options
      .load(this.value())
      .pipe(first(), timeout({ first: this.wait }))
      .subscribe({
        next: (value) => this.succeed(value),
        error: (error: unknown) => this.fail(error),
      });
  }

  reload(): void {
    this.isStopped.set(false);
    this.read();
  }

  stop(): void {
    this.isQueued = false;
    this.reading?.unsubscribe();
  }

  private nextAt(): number | null {
    const settled = this.settledAt();
    if (this.isStopped()) {
      return null;
    }
    if (settled === null) {
      return this.openedAt;
    }
    const base = this.isHidden() ? (this.options.hiddenEvery?.() ?? null) : this.options.every();
    if (base === null) {
      // Without any answer yet, a read once must not end on a transient failure (a 502 of a
      // redeployment): nothing would ever be shown, and no reload offered.
      const isUnanswered = this.lastSuccessAt() === null && this.failures() > 0;
      return isUnanswered ? settled + pollDelay(this.failures(), RETRY_MS) : null;
    }
    return settled + pollDelay(this.failures(), base);
  }

  private succeed(value: T): void {
    this.value.set(value);
    this.failure.set(null);
    this.failures.set(0);
    this.lastSuccessAt.set(Date.now());
    this.settle();
  }

  private fail(error: unknown): void {
    const failure = panelFailureOf(error);
    this.failure.set(failure);
    this.failures.update((count) => count + 1);
    if (failure.kind === SESSION) {
      this.isStopped.set(true);
    }
    this.settle();
  }

  // The next read waits for this one: a slow answer is never cut short by the next read.
  private settle(): void {
    this.reading = null;
    this.settledAt.set(Date.now());
    this.busy.set(false);
    if (this.isQueued) {
      this.isQueued = false;
      this.read();
    }
  }
}

/**
 * Reads a source over and over (§ 2.5), from the injection context of a component: at once,
 * then `every()` after each answer, never two reads in flight; `every()` null reads once,
 * trying again, backing off, until a first answer comes. A failure keeps the last value
 * on screen and backs off (`pollDelay`); a 401 or a 403 stops until `reload`. While the tab
 * is hidden the reads slow down to `hiddenEvery()`, or wait for it to show, then read at once
 * if the last answer is older than the cadence. Timers are plain `setTimeout`s, cleared with
 * the component: the admin renders in the browser only.
 */
export function injectLivePoll<T>(options: LivePollOptions<T>): LivePoll<T> {
  const isVisible = injectPageVisible();
  const reads = new LiveReads(
    options,
    inject(ADMIN_TIMEOUT),
    computed(() => !isVisible()),
  );
  effect((onCleanup) => {
    const at = reads.nextAttemptAt();
    if (at === null || reads.busy()) {
      return;
    }
    const timer = setTimeout(() => reads.read(), Math.max(0, at - Date.now()));
    onCleanup(() => clearTimeout(timer));
  });
  inject(DestroyRef).onDestroy(() => reads.stop());
  return {
    value: reads.value.asReadonly(),
    busy: reads.busy.asReadonly(),
    failure: reads.failure.asReadonly(),
    reload: () => reads.reload(),
    lastSuccessAt: reads.lastSuccessAt.asReadonly(),
    failures: reads.failures.asReadonly(),
    nextAttemptAt: reads.nextAttemptAt,
    isHidden: reads.isHidden,
  };
}
