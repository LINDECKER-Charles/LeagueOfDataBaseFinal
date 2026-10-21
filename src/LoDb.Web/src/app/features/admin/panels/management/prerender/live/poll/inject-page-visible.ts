import { DOCUMENT, DestroyRef, type Signal, inject, signal } from '@angular/core';

const HIDDEN = 'hidden';
const CHANGE = 'visibilitychange';

/**
 * Whether the tab shows, following `visibilitychange` until the caller is destroyed. The
 * admin renders in the browser only; a document without the API reads as visible.
 */
export function injectPageVisible(): Signal<boolean> {
  const document = inject(DOCUMENT);
  const isVisible = signal(document.visibilityState !== HIDDEN);
  const follow = () => isVisible.set(document.visibilityState !== HIDDEN);
  document.addEventListener(CHANGE, follow);
  inject(DestroyRef).onDestroy(() => document.removeEventListener(CHANGE, follow));
  return isVisible.asReadonly();
}
