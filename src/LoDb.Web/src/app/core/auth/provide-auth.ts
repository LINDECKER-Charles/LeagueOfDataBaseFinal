import { type EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';

/**
 * Providers of `core/auth` (L4.5): the session read on `/api/account/me`, the guards and the
 * `AuthStrategy` the platform picks. Registered by app.config.ts since L3.1, so that L4.5
 * fills it without touching the configuration; empty until then.
 */
export function provideAuth(): EnvironmentProviders {
  return makeEnvironmentProviders([]);
}
