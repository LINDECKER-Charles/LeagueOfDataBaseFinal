import { type EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';

/**
 * Beacon of the internal navigations (L7.1): at the end of every navigation but the first,
 * which the nginx mirror already counted, the browser posts `navigator.sendBeacon` to
 * `/api/analytics/view`. Registered by app.config.ts since L3.1, so that L7.1 fills it
 * without touching the configuration; empty until then.
 */
export function provideNavigationBeacon(): EnvironmentProviders {
  return makeEnvironmentProviders([]);
}
