import { InjectionToken } from '@angular/core';

/**
 * Whether the portal may sell packs and plans. The build's flag (`PAYMENTS_ENABLED`, false in
 * the store build of the apps, ADR 0007) lives in src/environments, which a feature may not
 * import: until core exposes it, the portal reads this token, true as in the web build, and
 * the store build provides `false`.
 */
export const PORTAL_PAYMENTS = new InjectionToken<boolean>('PORTAL_PAYMENTS', {
  providedIn: 'root',
  factory: () => true,
});
