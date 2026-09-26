import {
  type EnvironmentProviders,
  Injectable,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RELEASE_VERSION } from '../../../core/layout/shell/release-version';
import { ChangelogReader } from './changelog-reader';

@Injectable({ providedIn: 'root' })
class LoadedRelease {
  version: string | null = null;
}

/**
 * Feeds {@link RELEASE_VERSION} (the header chip and the footer) with the newest manifest
 * entry's version, read once before the first render, so the prerendered pages carry it too.
 * Registered in app.config.ts; without a manifest the token stays null and the chip hidden.
 */
export function provideReleaseVersion(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(async () => {
      const reader = inject(ChangelogReader);
      const loaded = inject(LoadedRelease);
      loaded.version = await firstValueFrom(reader.latestVersion());
    }),
    { provide: RELEASE_VERSION, useFactory: () => inject(LoadedRelease).version },
  ]);
}
