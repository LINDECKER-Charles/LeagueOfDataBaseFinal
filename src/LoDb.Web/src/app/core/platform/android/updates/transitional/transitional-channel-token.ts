import { InjectionToken } from '@angular/core';
import { environment } from '../../../../../../environments/environment';
import type { TransitionalSettings } from './transitional-settings';

/**
 * The flag of the transitional channel (ADR 0008): set it to false once Play serves the app,
 * whose installations then update through Play alone. Installations from Play never use it.
 */
const TRANSITIONAL_CHANNEL_ENABLED = true;
// Served by the site next to its API (docs/guides/release-android.md): the WebView may read
// it, where a GitHub Release asset has no CORS headers. The APK itself is a release asset,
// which the system browser downloads.
const MANIFEST_PATH = '/android/latest.json';
const RELEASE_HOST = 'github.com';

/** The settings of the transitional channel; null when it is off, as in the web build. */
export const TRANSITIONAL_CHANNEL = new InjectionToken<TransitionalSettings | null>(
  'TRANSITIONAL_CHANNEL',
  {
    providedIn: 'root',
    factory: () => {
      const origin = environment.publicApiOrigin;
      if (!TRANSITIONAL_CHANNEL_ENABLED || origin === null) {
        return null;
      }
      return {
        manifestUrl: `${origin}${MANIFEST_PATH}`,
        apkHosts: [new URL(origin).host, RELEASE_HOST],
      };
    },
  },
);
