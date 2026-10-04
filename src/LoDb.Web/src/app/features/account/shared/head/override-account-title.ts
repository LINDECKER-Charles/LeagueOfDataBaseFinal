import { DestroyRef, effect, inject } from '@angular/core';
import { AccountHead } from './account-head';
import type { AccountTitle } from './account-title';

/**
 * Names the account page by `title` while the calling view lives, and gives the route its
 * title back when the view leaves. Called in the constructor of a view of AccountPage; a
 * null title keeps the route's.
 */
export function overrideAccountTitle(title: () => AccountTitle | null): void {
  const head = inject(AccountHead);
  effect(() => head.override.set(title()));
  inject(DestroyRef).onDestroy(() => head.override.set(null));
}
