import { isPlatformBrowser } from '@angular/common';
import {
  type EnvironmentProviders,
  Injector,
  PLATFORM_ID,
  afterNextRender,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
  provideEnvironmentInitializer,
} from '@angular/core';
import { CookieAuthStrategy } from './cookie/cookie-auth-strategy';
import { AuthSession } from './session/auth-session';
import { AuthStrategies } from './strategy/auth-strategies';

// The first read of the session waits for the first render: the hydrated page never waits
// on `/api/account/me`, and the platform detection has completed by then. A failure is
// dropped: `status` stays `unknown` and the next `load()`, a guard's, asks again.
function readSessionAfterFirstRender(): void {
  if (!isPlatformBrowser(inject(PLATFORM_ID))) {
    return;
  }
  const injector = inject(Injector);
  afterNextRender(
    () => {
      injector
        .get(AuthSession)
        .load()
        .catch(() => undefined);
    },
    { injector },
  );
}

/**
 * Providers of `core/auth` (L4.5): the `cookie` strategy of the web, registered in
 * `AuthStrategies` next to those the platforms register, and the first read of the session
 * in the browser. `AuthSession`, `AUTH_STRATEGY` and the guards are provided in root.
 */
export function provideAuth(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideEnvironmentInitializer(() => {
      inject(AuthStrategies).register('cookie', CookieAuthStrategy);
    }),
    provideAppInitializer(readSessionAfterFirstRender),
  ]);
}
