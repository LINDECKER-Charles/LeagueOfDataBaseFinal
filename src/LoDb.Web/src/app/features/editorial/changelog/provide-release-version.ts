import {
  type EnvironmentProviders,
  Injectable,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RELEASE_VERSION } from '../../../core/layout/shell/release-version';
import { ActivePlatform } from '../../../core/platform/detection/active-platform';
import { ChangelogReader } from './changelog-reader';

@Injectable({ providedIn: 'root' })
class LoadedRelease {
  version: string | null = null;
}

/**
 * Feeds {@link RELEASE_VERSION} (the header chip and the footer) with the newest manifest
 * entry's version, read once before the first render, so the prerendered pages carry it too.
 * Registered in app.config.ts; without a manifest the token stays null and the chip hidden.
 *
 * The read waits for the platform detection, another initializer: every request passes
 * interceptors that inject `PLATFORM`, and injecting it before the detection ends fails
 * that token for the whole application.
 */
export function provideReleaseVersion(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(async () => {
      const platform = inject(ActivePlatform);
      const reader = inject(ChangelogReader);
      const loaded = inject(LoadedRelease);
      await platform.detect();
      loaded.version = await firstValueFrom(reader.latestVersion());
    }),
    { provide: RELEASE_VERSION, useFactory: () => inject(LoadedRelease).version },
  ]);
}
