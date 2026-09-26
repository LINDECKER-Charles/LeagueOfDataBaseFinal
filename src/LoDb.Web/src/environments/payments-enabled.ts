import { InjectionToken } from '@angular/core';
import { environment } from './environment';

/**
 * Whether the build may show a payment, read from the build environment (`payments`). The
 * routes, the header and the footer read this token rather than the environment, so a test
 * provides the store build's `false` without swapping files.
 */
export const PAYMENTS_ENABLED = new InjectionToken<boolean>('PAYMENTS_ENABLED', {
  providedIn: 'root',
  factory: () => environment.payments,
});
