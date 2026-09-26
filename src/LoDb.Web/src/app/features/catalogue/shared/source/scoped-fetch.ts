import { DestroyRef, type Signal, effect, inject, signal, untracked } from '@angular/core';
import type { Observable, Subscription } from 'rxjs';

/**
 * The latest answer of a fetch keyed by a scope, refetched when the scope changes. Unlike a
 * resource, it subscribes at once: an answer the HTTP transfer cache holds is there for the
 * first render, so hydration finds the server-rendered page instead of redrawing it.
 */
export function scopedFetch<S, T>(
  scope: Signal<S | undefined>,
  fetch: (at: S) => Observable<T>,
): Signal<T | undefined> {
  const value = signal<T | undefined>(undefined);
  let subscription: Subscription | null = null;
  const load = (at: S | undefined) => {
    subscription?.unsubscribe();
    value.set(undefined);
    subscription = at === undefined ? null : fetch(at).subscribe((next) => value.set(next));
  };
  let loaded = untracked(scope);
  load(loaded);
  effect(() => {
    const at = scope();
    if (at !== loaded) {
      loaded = at;
      untracked(() => load(at));
    }
  });
  inject(DestroyRef).onDestroy(() => subscription?.unsubscribe());
  return value.asReadonly();
}
