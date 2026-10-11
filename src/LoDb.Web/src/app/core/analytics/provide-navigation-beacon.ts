import {
  type EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { AudienceTracker } from './audience-tracker';
import { NavigationBeacon } from './navigation-beacon';

/**
 * The audience of the site, the hook point of `app.config.ts` for `core/analytics`. Two
 * measures run side by side until the first-party one is checked against the other:
 *
 * - the beacon of the internal navigations (L7.1): at the end of every navigation but the
 *   first, which the nginx mirror already counted, the browser posts `navigator.sendBeacon`
 *   to `/api/analytics/view` (`NavigationBeacon`);
 * - the first-party audience (`AudienceTracker`): every page view, the first one included,
 *   and a few events, posted to Umami behind the site's own `/u/`.
 *
 * Both listen before the initial navigation starts, so that they know which one is first.
 */
export function provideNavigationBeacon(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() => inject(NavigationBeacon).start()),
    provideAppInitializer(() => inject(AudienceTracker).start()),
  ]);
}
