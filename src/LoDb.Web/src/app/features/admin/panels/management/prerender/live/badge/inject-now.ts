import { type Signal, effect, signal } from '@angular/core';
import { injectPageVisible } from '../poll/inject-page-visible';

const SECOND_MS = 1_000;

/**
 * Date.now(), moved on every `every` milliseconds while the tab shows, from the injection
 * context of a component: « il y a 4 s » advances without a read. Suspended while hidden,
 * caught up at once on the tab's return; the timer goes with the component.
 */
export function injectNow(every = SECOND_MS): Signal<number> {
  const isVisible = injectPageVisible();
  const now = signal(Date.now());
  effect((onCleanup) => {
    if (!isVisible()) {
      return;
    }
    const tick = () => now.set(Date.now());
    tick();
    const timer = setInterval(tick, every);
    onCleanup(() => clearInterval(timer));
  });
  return now.asReadonly();
}
