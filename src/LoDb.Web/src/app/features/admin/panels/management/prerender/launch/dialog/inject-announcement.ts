import { type Signal, effect, signal } from '@angular/core';

// A selection typed at the keyboard changes the text at each key: say it once it settles.
const SETTLE_MS = 500;

/**
 * The text of an `aria-live` region, 500 ms after `text` last changed (§ 3.2, § 3.3.4), so
 * that a screen reader is not drowned in counts while the operator selects. Empty until the
 * first change: opening the dialog announces its title, not a count.
 */
export function injectAnnouncement(text: () => string): Signal<string> {
  const announced = signal('');
  let isFirst = true;
  effect((onCleanup) => {
    const next = text();
    if (isFirst) {
      isFirst = false;
      return;
    }
    const timer = setTimeout(() => announced.set(next), SETTLE_MS);
    onCleanup(() => clearTimeout(timer));
  });
  return announced.asReadonly();
}
