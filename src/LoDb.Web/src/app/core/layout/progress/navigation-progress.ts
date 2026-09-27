import { isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  PLATFORM_ID,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationStart,
  type Event as RouterEvent,
  Router,
} from '@angular/router';

/** Where the bar is: absent, drawn empty, creeping towards its end, or completing. */
type Progress = 'idle' | 'started' | 'pending' | 'done';

// Turbo Drive's own delay (legacy site): a navigation faster than this shows nothing.
const SHOW_AFTER_MS = 500;
// One frame at the empty width, so that the creep is a transition from it.
const CREEP_AFTER_MS = 20;
// The completing sweep and fade of the stylesheet (`hx-nav-progress--done`).
const DONE_FOR_MS = 400;
// The browser's first navigation hydrates the server's page: nothing to wait for.
const FIRST_NAVIGATION_ID = 1;

function isSettled(
  event: RouterEvent,
): event is NavigationEnd | NavigationCancel | NavigationError {
  return (
    event instanceof NavigationEnd ||
    event instanceof NavigationCancel ||
    event instanceof NavigationError
  );
}

/**
 * The thin bar along the top of the viewport while a navigation waits for its data, as the
 * legacy site's Turbo Drive drew it: it only appears after half a second, creeps on while
 * the resolvers run, then sweeps to the end and fades. Decorative, hidden from assistive
 * technology, and browser only.
 */
@Component({
  selector: 'lodb-navigation-progress',
  template: `@if (progress() !== 'idle') {
    <div
      class="hx-nav-progress"
      [class.hx-nav-progress--pending]="progress() === 'pending'"
      [class.hx-nav-progress--done]="progress() === 'done'"
      aria-hidden="true"
    ></div>
  }`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavigationProgress {
  protected readonly progress = signal<Progress>('idle');
  private timer: ReturnType<typeof setTimeout> | undefined;
  // The navigation the bar stands for: one it superseded settles without ending the bar.
  private navigation = 0;

  constructor() {
    if (!isPlatformBrowser(inject(PLATFORM_ID))) {
      return;
    }
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
    inject(Router)
      .events.pipe(takeUntilDestroyed())
      .subscribe((event) => this.follow(event));
  }

  private follow(event: RouterEvent): void {
    if (event instanceof NavigationStart) {
      this.navigation = event.id;
      if (event.id > FIRST_NAVIGATION_ID && !this.isRunning()) {
        this.later(SHOW_AFTER_MS, () => this.start());
      }
    } else if (isSettled(event) && event.id === this.navigation) {
      this.settle();
    }
  }

  private isRunning(): boolean {
    const progress = this.progress();
    return progress === 'started' || progress === 'pending';
  }

  private start(): void {
    this.progress.set('started');
    this.later(CREEP_AFTER_MS, () => this.progress.set('pending'));
  }

  private settle(): void {
    if (this.progress() === 'idle') {
      clearTimeout(this.timer);
      return;
    }
    this.progress.set('done');
    this.later(DONE_FOR_MS, () => this.progress.set('idle'));
  }

  private later(delay: number, run: () => void): void {
    clearTimeout(this.timer);
    this.timer = setTimeout(run, delay);
  }
}
