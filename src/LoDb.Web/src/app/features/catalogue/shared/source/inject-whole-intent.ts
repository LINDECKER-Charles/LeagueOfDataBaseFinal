import { DOCUMENT, DestroyRef, type WritableSignal, inject, signal } from '@angular/core';
import { connectionOf } from '../network/connection-of';
import { wantsLightData } from '../network/wants-light-data';
import { whenIdleAfterLoad } from '../network/when-idle-after-load';

/**
 * Whether a list page wants its whole list yet, to call from an injection context. It turns
 * true at once when the reader shows they will filter (whoever holds it sets it), else once
 * the page has loaded and the browser is idle, the first page painted. Under Save-Data or on
 * a slow connection, only the reader's intent reads it. `isNeeded` is false on the server,
 * and where the first answer already is the whole list.
 */
export function injectWholeIntent(isNeeded: boolean): WritableSignal<boolean> {
  const wanted = signal(false);
  const view = inject(DOCUMENT).defaultView;
  if (!isNeeded || view === null || wantsLightData(connectionOf(view))) {
    return wanted;
  }
  const cancel = whenIdleAfterLoad(view, () => wanted.set(true));
  inject(DestroyRef).onDestroy(cancel);
  return wanted;
}
