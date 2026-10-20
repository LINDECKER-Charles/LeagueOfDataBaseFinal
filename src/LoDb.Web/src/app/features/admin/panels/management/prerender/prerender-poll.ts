import { InjectionToken } from '@angular/core';

// A pass writes a few pages a second for days: a look every 20 seconds follows it closely
// enough without loading the API.
const POLL_MS = 20_000;

/**
 * How long the panel waits, once a read has answered, before it reads the pre-rendering
 * again while a pass runs, in milliseconds. A token so that a spec can poll at once.
 */
export const PRERENDER_POLL = new InjectionToken<number>('PRERENDER_POLL', {
  providedIn: 'root',
  factory: () => POLL_MS,
});
