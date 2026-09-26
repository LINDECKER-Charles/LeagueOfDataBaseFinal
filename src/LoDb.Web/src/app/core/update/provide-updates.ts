import { type EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';

/**
 * Providers of `core/update` (L9.0), registered by app.config.ts since L3.1. `ClientUpdate`,
 * which the interceptor feeds and the blocking screen reads, is provided in root, and the
 * update state comes from `PLATFORM`: nothing else to register, so that the web, which
 * never updates this way, pays nothing at startup.
 */
export function provideUpdates(): EnvironmentProviders {
  return makeEnvironmentProviders([]);
}
