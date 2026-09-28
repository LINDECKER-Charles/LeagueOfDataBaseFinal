import type { PendingGoogleSignIn } from './pending-google-sign-in';

const GOOGLE_AUTHORIZATION_ENDPOINT = 'https://accounts.google.com/o/oauth2/v2/auth';
const SCOPE = 'openid email profile';
// Lets a user with several Google accounts pick one, instead of the browser's current one.
const ACCOUNT_CHOOSER = 'select_account';

/**
 * Google's authorization request of an installed app: authorization code with PKCE, opened in
 * the system browser (RFC 8252), as the desktop host does with its loopback redirect.
 */
export function googleAuthorizationUrl(flow: PendingGoogleSignIn, challenge: string): string {
  const url = new URL(GOOGLE_AUTHORIZATION_ENDPOINT);
  url.search = new URLSearchParams({
    client_id: flow.clientId,
    redirect_uri: flow.redirectUri,
    response_type: 'code',
    scope: SCOPE,
    code_challenge: challenge,
    code_challenge_method: 'S256',
    state: flow.state,
    prompt: ACCOUNT_CHOOSER,
  }).toString();
  return url.href;
}
