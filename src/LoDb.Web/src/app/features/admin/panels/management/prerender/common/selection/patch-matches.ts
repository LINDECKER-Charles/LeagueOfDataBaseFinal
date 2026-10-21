const SEPARATORS = /[\s,]+/;
const WILDCARD = '*';

function matchesToken(version: string, token: string): boolean {
  if (token.endsWith(WILDCARD)) {
    return version.startsWith(token.slice(0, -WILDCARD.length));
  }
  return version === token || version.startsWith(`${token}.`);
}

/**
 * Whether `version` passes the text filter of a list of patches (§ 3.3.3): the query splits
 * on spaces and commas, and a version passes when it equals a token or starts with it and a
 * point (« 15 » takes the season, « 16.1 » takes 16.1.1 and not 16.10.1); a token ending
 * with `*` is a raw prefix (« 16.1* » takes both). An empty query lets everything through.
 */
export function patchMatches(version: string, query: string): boolean {
  const tokens = query.split(SEPARATORS).filter((token) => token !== '');
  return tokens.length === 0 || tokens.some((token) => matchesToken(version, token));
}
