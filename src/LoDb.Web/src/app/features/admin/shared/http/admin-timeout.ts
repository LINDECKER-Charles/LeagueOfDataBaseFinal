import { InjectionToken } from '@angular/core';

// The legacy back office cut its panels off at 30 seconds too, and offered to try again.
const TIMEOUT_MS = 30_000;

/**
 * How long the admin waits for the API, in milliseconds, before giving up on a panel or an
 * action. A token so that a spec can wait less.
 */
export const ADMIN_TIMEOUT = new InjectionToken<number>('ADMIN_TIMEOUT', {
  providedIn: 'root',
  factory: () => TIMEOUT_MS,
});
