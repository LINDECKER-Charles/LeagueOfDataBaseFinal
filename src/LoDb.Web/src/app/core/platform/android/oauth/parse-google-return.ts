import type { GoogleReturn } from './google-return';

function parsedUrl(url: string): URL | null {
  try {
    return new URL(url);
  } catch {
    return null;
  }
}

/**
 * Reads the App Link Google sent back to, or null when `url` is not the redirect URI: the
 * app may one day open other links, which are none of the sign-in's business.
 */
export function parseGoogleReturn(url: string, redirectUri: string): GoogleReturn | null {
  const opened = parsedUrl(url);
  const expected = new URL(redirectUri);
  if (opened?.origin !== expected.origin || opened.pathname !== expected.pathname) {
    return null;
  }
  const query = opened.searchParams;
  return { state: query.get('state'), code: query.get('code'), error: query.get('error') };
}
