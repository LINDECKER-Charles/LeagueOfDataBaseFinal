const WEB_SCHEMES = ['http:', 'https:'];

/**
 * The referrer of a visit reduced to its origin (`https://www.google.com`): never a path or
 * a query, which can carry a search or a token. Empty for a referrer on the site itself, a
 * scheme other than http(s), or none at all.
 */
export function audienceReferrerOf(referrer: string, ownHost: string): string {
  try {
    const url = new URL(referrer);
    return WEB_SCHEMES.includes(url.protocol) && url.hostname !== ownHost ? url.origin : '';
  } catch {
    return '';
  }
}
