import type { UrlMatchResult, UrlSegment } from '@angular/router';

const PAGE = 'reset-password';

/**
 * `reset-password`, and `reset-password/{token}` of the links sent before the token moved to
 * the fragment: one route, so that the page stays when it takes the token out of the address.
 */
export function resetPasswordMatcher(segments: UrlSegment[]): UrlMatchResult | null {
  const [page, token, ...rest] = segments;
  if (page?.path !== PAGE || rest.length > 0) {
    return null;
  }
  return token === undefined
    ? { consumed: [page] }
    : { consumed: [page, token], posParams: { token } };
}
