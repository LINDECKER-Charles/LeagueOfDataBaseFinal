import { DOCUMENT, InjectionToken, inject } from '@angular/core';
import { environment } from '../../../../../environments/environment';
import type { AndroidGoogleClient } from './android-google-client';

// Under the `/app/oauth/` prefix the manifest verifies as an App Link (L10.1).
const REDIRECT_PATH = '/app/oauth/google';

// `AppEnvironment` names no Google client yet. Read defensively, so that the build which
// adds `googleClientId` to the shell environments turns the sign-in on without this file.
function configuredClientId(): string | null {
  const settings: object = environment;
  return 'googleClientId' in settings && typeof settings.googleClientId === 'string'
    ? settings.googleClientId
    : null;
}

/**
 * The App Link host is the site's: the manifest defaults `lodbAppLinkHost` to it, and it is
 * the public origin of the API the shell build names.
 */
export const ANDROID_GOOGLE_CLIENT = new InjectionToken<AndroidGoogleClient>(
  'ANDROID_GOOGLE_CLIENT',
  {
    providedIn: 'root',
    factory: () => {
      const siteOrigin = environment.publicApiOrigin ?? inject(DOCUMENT).location.origin;
      return {
        clientId: configuredClientId(),
        redirectUri: new URL(REDIRECT_PATH, siteOrigin).href,
      };
    },
  },
);
