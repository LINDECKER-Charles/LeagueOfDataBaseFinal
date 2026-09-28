import {
  type EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { NavigationBeacon } from './navigation-beacon';

/**
 * Beacon of the internal navigations (L7.1): at the end of every navigation but the first,
 * which the nginx mirror already counted, the browser posts `navigator.sendBeacon` to
 * `/api/analytics/view` (`NavigationBeacon`). It listens before the initial navigation
 * starts, so that it knows which one is first.
 */
export function provideNavigationBeacon(): EnvironmentProviders {
  return makeEnvironmentProviders([provideAppInitializer(() => inject(NavigationBeacon).start())]);
}
