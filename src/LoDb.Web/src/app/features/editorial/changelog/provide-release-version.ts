import {
  type EnvironmentProviders,
  Injectable,
  TransferState,
  inject,
  makeEnvironmentProviders,
  makeStateKey,
  provideAppInitializer,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { injectSsrRequestContext } from '../../../core/http/inject-ssr-request-context';
import { RELEASE_VERSION } from '../../../core/layout/shell/release-version';
import { ActivePlatform } from '../../../core/platform/detection/active-platform';
import { CHANGELOG_MANIFEST_PATH } from './changelog-manifest-path';
import { ChangelogReader } from './changelog-reader';
import { parseManifest } from './parsing/parse-manifest';

// The version alone travels in the page, not the manifest it comes from.
const RELEASE_STATE = makeStateKey<string | null>('lodb.release-version');

@Injectable({ providedIn: 'root' })
class LoadedRelease {
  version: string | null = null;
}

/**
 * Where the version comes from: the manifest the server read at start-up, then what the page
 * the server rendered carries, then the manifest over HTTP (the shell builds, the prerender).
 * Injected at once, before anything is awaited.
 */
function injectReleaseReader(): () => Promise<string | null> {
  const files = injectSsrRequestContext()?.buildFiles ?? null;
  const state = inject(TransferState);
  const platform = inject(ActivePlatform);
  const reader = inject(ChangelogReader);
  return async () => {
    if (files !== null) {
      return parseManifest(files.get(CHANGELOG_MANIFEST_PATH))[0]?.version ?? null;
    }
    if (state.hasKey(RELEASE_STATE)) {
      return state.get(RELEASE_STATE, null);
    }
    // Every request passes interceptors that inject `PLATFORM`: injecting it before the
    // detection, another initializer, ends fails that token for the whole application.
    await platform.detect();
    return firstValueFrom(reader.latestVersion());
  };
}

/**
 * Feeds {@link RELEASE_VERSION} (the header chip and the footer) with the newest manifest
 * entry's version, known before the first render, so the prerendered pages carry it too.
 * Registered in app.config.ts; without a manifest the token stays null and the chip hidden.
 * A server render reads it from the build files held in memory and hands the browser the
 * version alone, which spares every page the request and the whole manifest in its state.
 */
export function provideReleaseVersion(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(async () => {
      const read = injectReleaseReader();
      const loaded = inject(LoadedRelease);
      const state = inject(TransferState);
      loaded.version = await read();
      state.set(RELEASE_STATE, loaded.version);
    }),
    { provide: RELEASE_VERSION, useFactory: () => inject(LoadedRelease).version },
  ]);
}
