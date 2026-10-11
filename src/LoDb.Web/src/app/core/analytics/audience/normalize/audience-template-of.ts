import type { ActivatedRouteSnapshot } from '@angular/router';
import { withHomeSlash } from '../../../routing/locale/with-home-slash';

const SEGMENT_SEPARATOR = '/';
const PARAM_PREFIX = ':';
// Closed sets, checked by their guards before a route matches: their values say nothing of
// the visitor, and the locale and the version are what the audience is cut by.
const SPELLED_PARAMS: readonly string[] = ['locale', 'version'];

function segmentOf(segment: string, route: ActivatedRouteSnapshot): string {
  const name = segment.startsWith(PARAM_PREFIX) ? segment.slice(PARAM_PREFIX.length) : null;
  if (name === null || !SPELLED_PARAMS.includes(name)) {
    return segment;
  }
  return route.params[name] ?? segment;
}

/**
 * The template of the matched routes (`/fr/u/:username`, `/b/:token`, `/fr/legal/privacy`):
 * the locale and the version spelled out, every other parameter left as its name, so that no
 * pseudonym, token or id reaches the audience.
 */
export function audienceTemplateOf(chain: readonly ActivatedRouteSnapshot[]): string {
  const segments = chain.flatMap((route) =>
    (route.routeConfig?.path ?? '')
      .split(SEGMENT_SEPARATOR)
      .filter((segment) => segment !== '')
      .map((segment) => segmentOf(segment, route)),
  );
  return withHomeSlash(SEGMENT_SEPARATOR + segments.join(SEGMENT_SEPARATOR));
}
