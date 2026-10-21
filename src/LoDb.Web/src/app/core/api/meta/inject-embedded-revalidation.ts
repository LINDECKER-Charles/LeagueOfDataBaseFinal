import { isPlatformBrowser } from '@angular/common';
import { ApplicationRef, PLATFORM_ID, TransferState, inject } from '@angular/core';
import {
  EMPTY,
  type MonoTypeOperatorFunction,
  type Observable,
  catchError,
  concat,
  filter,
  from,
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

/**
 * Stale while revalidate, for an answer the HTTP transfer cache may take from the page's
 * `ng-state`. The source's first answer passes as it is, so hydration renders what the
 * server rendered and nothing is asked before the first paint; once the application is
 * stable, `refetch` asks once more and its answer follows only when it differs. A page kept
 * for months (an old patch, pre-rendered or archived) would otherwise hold its render-time
 * answer for the whole visit. A failed refetch keeps that answer, and nothing asks again.
 * The server, and a page that embeds nothing (the desktop and Android shells start empty),
 * never refetch.
 */
export function injectEmbeddedRevalidation(): <T>(
  refetch: () => Observable<T>,
) => MonoTypeOperatorFunction<T> {
  const appRef = inject(ApplicationRef);
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  // Read once, at startup: the state is still the page's, before any reader removes a key.
  const mayBeEmbedded = isBrowser && !inject(TransferState).isEmpty;
  return <T>(refetch: () => Observable<T>) =>
    (source: Observable<T>) => {
      if (!mayBeEmbedded) {
        return source;
      }
      const fresh$ = from(appRef.whenStable()).pipe(switchMap(refetch));
      return source.pipe(switchMap((embedded) => revalidated(embedded, fresh$)));
    };
}
