import { effect, inject } from '@angular/core';
import type { Panel } from '../../../state/panel';
import { PRERENDER_POLL } from './prerender-poll';

/**
 * Reads `panel` again `PRERENDER_POLL` milliseconds after its last load ended, for as long as
 * `isActive` holds, from the injection context of a component. The next read waits for the
 * last one to answer, so a slow answer is never cut short by the next read; a failed read
 * schedules the next one too. The timer stops when `isActive` turns false and when the
 * component is destroyed. The admin renders in the browser only, so no server holds it.
 */
export function pollWhile(isActive: () => boolean, panel: Panel<unknown>): void {
  const every = inject(PRERENDER_POLL);
  effect((onCleanup) => {
    if (!isActive() || panel.busy()) {
      return;
    }
    const timer = setTimeout(() => panel.reload(), every);
    onCleanup(() => clearTimeout(timer));
  });
}
