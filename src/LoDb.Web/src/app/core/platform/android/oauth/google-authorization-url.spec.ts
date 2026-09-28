import { googleAuthorizationUrl } from './google-authorization-url';
import type { PendingGoogleSignIn } from './pending-google-sign-in';

const flow: PendingGoogleSignIn = {
  clientId: 'client.apps.googleusercontent.com',
  redirectUri: 'https://league-of-data-base.com/app/oauth/google',
  state: 'state-1',
  verifier: 'verifier-1',
  landingUrl: '/fr/account/profile',
  locale: 'fr',
  isRemembered: true,
  startedAt: 0,
};

describe('googleAuthorizationUrl', () => {
  it('asks Google for a code with PKCE, returned to the App Link', () => {
    const url = new URL(googleAuthorizationUrl(flow, 'challenge-1'));

    expect(`${url.origin}${url.pathname}`).toBe('https://accounts.google.com/o/oauth2/v2/auth');
    expect(Object.fromEntries(url.searchParams)).toEqual({
      client_id: 'client.apps.googleusercontent.com',
      redirect_uri: 'https://league-of-data-base.com/app/oauth/google',
      response_type: 'code',
      scope: 'openid email profile',
      code_challenge: 'challenge-1',
      code_challenge_method: 'S256',
      state: 'state-1',
      prompt: 'select_account',
    });
  });

  it('never sends the verifier to Google', () => {
    expect(googleAuthorizationUrl(flow, 'challenge-1')).not.toContain('verifier-1');
  });
});
