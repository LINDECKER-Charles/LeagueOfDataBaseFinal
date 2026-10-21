import { isPlatformBrowser } from '@angular/common';
import { ApplicationRef, PLATFORM_ID, inject } from '@angular/core';
import {
  EMPTY,
  type MonoTypeOperatorFunction,
  Observable,
  catchError,
  concat,
  defer,
  filter,
  of,
  switchMap,
} from 'rxjs';

// Answers of the same request, parsed from the same API: equal documents serialize alike.
function isSameDocument(fresh: unknown, embedded: unknown): boolean {
  return JSON.stringify(fresh) === JSON.stringify(embedded);
}

// The embedded answer at once, then the fresh one if it differs; a failure keeps the first.
function revalidated<T>(embedded: T, fresh$: Observable<T>): Observable<T> {
  return concat(
    of(embedded),
    fresh$.pipe(
      filter((fresh) => !isSameDocument(fresh, embedded)),
      catchError(() => EMPTY),
    ),
  );
}

// The transfer cache replays the page's `ng-state` within the subscription, while the
// network always answers later: only an answer given at once comes from the page. A page
// that embeds none (a prerendered one, a shell) thus asks the network once, not twice.
function revalidatedIfEmbedded<T>(source: Observable<T>, fresh$: Observable<T>): Observable<T> {
  return new Observable<T>((subscriber) => {
    let isSubscribing = true;
    const subscription = source
      .pipe(switchMap((first) => (isSubscribing ? revalidated(first, fresh$) : of(first))))
      .subscribe(subscriber);
    isSubscribing = false;
    return subscription;
  });
}

/**
 * Stale while revalidate, for an answer the HTTP transfer cache may take from the page's
 * `ng-state`. The source's first answer passes as it is, so hydration renders what the
 * server rendered and nothing is asked before the first paint; when the page gave that
 * answer, `refetch` asks once more once the application is stable, and its answer follows
 * only when it differs. A page kept for months (an old patch, pre-rendered or archived)
 * would otherwise hold its render-time answer for the whole visit. A failed refetch keeps
 * that answer, and nothing asks again. The server never refetches.
 */
export function injectEmbeddedRevalidation(): <T>(
  refetch: () => Observable<T>,
) => MonoTypeOperatorFunction<T> {
  const appRef = inject(ApplicationRef);
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  return <T>(refetch: () => Observable<T>) =>
    (source: Observable<T>) => {
      if (!isBrowser) {
        return source;
      }
      // Asked when the page has answered: stable then means hydrated, not merely booting.
      const fresh$ = defer(() => appRef.whenStable()).pipe(switchMap(refetch));
      return revalidatedIfEmbedded(source, fresh$);
    };
}
