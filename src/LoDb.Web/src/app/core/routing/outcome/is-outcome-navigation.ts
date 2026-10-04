import { inject } from '@angular/core';
import { Router, type CanMatchFn } from '@angular/router';
import { pendingOutcome } from './pending-outcome';

/**
 * Lets the error route match whatever the URL when the navigation carries an outcome, so a
 * redirect, a 404 or a failure renders at the requested address: the SSR engine answers a
 * changed URL with a 302 of its own, which is never the status wanted.
 */
export const isOutcomeNavigation: CanMatchFn = () => pendingOutcome(inject(Router)) !== null;
