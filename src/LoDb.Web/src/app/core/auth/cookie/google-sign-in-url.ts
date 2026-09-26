import {
  type StartGoogleSignIn$Params,
  startGoogleSignIn,
} from '../../api/generated/fn/account/start-google-sign-in';

/**
 * The address of `GET /api/account/google/start`, which the browser opens as a page: Google
 * refuses to be called from script. The parameters are typed by the generated client, so a
 * renamed one breaks the build rather than the sign-in.
 */
export function googleSignInUrl(apiOrigin: string, params: StartGoogleSignIn$Params): string {
  const url = new URL(startGoogleSignIn.PATH, apiOrigin);
  for (const [name, value] of Object.entries(params)) {
    if (value !== undefined) {
      url.searchParams.set(name, String(value));
    }
  }
  return url.href;
}
