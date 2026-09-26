import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, RESPONSE_INIT, inject } from '@angular/core';
import { type RedirectCommand, type ResolveFn, Router } from '@angular/router';
import { firstValueFrom, isObservable } from 'rxjs';
import { CATALOGUE_METRIC } from './catalogue-metric';
import { LoadClock } from './load-clock';

type Resolved<T> = T | RedirectCommand;

function appendServerTiming(init: ResponseInit, durationMs: number): void {
  const headers = init.headers instanceof Headers ? init.headers : new Headers(init.headers);
  init.headers = headers;
  headers.append('Server-Timing', `${CATALOGUE_METRIC};dur=${durationMs.toFixed(1)}`);
}

/**
 * A detail resolver that times itself. On the server, the time the entity took goes out as
 * `Server-Timing: catalogue;dur=…`, which the browser exposes on its Navigation Timing entry;
 * in the browser, it is noted in LoadClock for the load-time badge. The inner resolver is
 * called before any wait, in the injection context it needs.
 */
export function withLoadTiming<T>(resolve: ResolveFn<T>): ResolveFn<T> {
  return async (route, state) => {
    const init = inject(RESPONSE_INIT, { optional: true });
    const clock = inject(LoadClock);
    const inBrowser = isPlatformBrowser(inject(PLATFORM_ID));
    const initial = !inject(Router).navigated;
    const startedAt = performance.now();
    const pending = resolve(route, state);
    const resolved: Resolved<T> = isObservable(pending)
      ? await firstValueFrom(pending)
      : await pending;
    const fetchMs = performance.now() - startedAt;
    if (init !== null) {
      appendServerTiming(init, fetchMs);
    }
    if (inBrowser) {
      clock.last.set({ initial, startedAt, fetchMs });
    }
    return resolved;
  };
}
