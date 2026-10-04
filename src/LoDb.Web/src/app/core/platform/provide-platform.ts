import {
  type EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { ActivePlatform } from './detection/active-platform';
import { PLATFORM } from './platform';

/** Detects the platform before the first navigation and exposes it as `PLATFORM`. */
export function providePlatform(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(() => inject(ActivePlatform).detect()),
    { provide: PLATFORM, useFactory: () => inject(ActivePlatform).get() },
  ]);
}
