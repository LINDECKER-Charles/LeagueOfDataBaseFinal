import { type EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';

/**
 * Providers of `core/update` (L9.0): the client policy of the apps and the state that drives
 * the blocking screen and the restart banner. Registered by app.config.ts since L3.1, so that
 * L9.0 fills it without touching the configuration; empty until then.
 */
export function provideUpdates(): EnvironmentProviders {
  return makeEnvironmentProviders([]);
}
