import { InjectionToken } from '@angular/core';

/**
 * Whether the portal may sell packs and plans. The build's flag (`PAYMENTS_ENABLED`, false in
 * the store build of the apps, ADR 0007) lives in src/environments, which a feature may not
 * import: app.config.ts provides this token from it. The default, true as in the web build,
 * only serves a test that leaves it out.
 */
export const PORTAL_PAYMENTS = new InjectionToken<boolean>('PORTAL_PAYMENTS', {
  providedIn: 'root',
  factory: () => true,
});
