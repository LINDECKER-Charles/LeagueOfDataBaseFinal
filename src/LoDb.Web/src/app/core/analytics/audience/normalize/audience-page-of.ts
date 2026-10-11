import { HttpStatusCode } from '@angular/common/http';
import { type ActivatedRouteSnapshot, PRIMARY_OUTLET } from '@angular/router';
import type { PageOutcome } from '../../../routing/outcome/page-outcome';
import { withHomeSlash } from '../../../routing/locale/with-home-slash';
import { QueryString } from '../../../routing/url/query-string';
import { pageKeyOf } from '../../page-key-of';
import type { AudiencePage } from '../audience-page';
import { audienceQueryOf } from './audience-query-of';
import { AUDIENCE_ROUTE_DATA } from '../audience-route-data';
import { audienceTemplateOf } from './audience-template-of';

const QUERY_START = '?';
const FRAGMENT_START = '#';
const OUTCOME_KEY = 'outcome';
const LOCALE_PARAM = 'locale';
const VERSION_PARAM = 'version';
// The list search of ADR 0005 (`?q=`): its text stays here, only its start is counted.
const SEARCH_PARAM = 'q';
const CURRENT_VERSION = 'current';

function chainOf(root: ActivatedRouteSnapshot): ActivatedRouteSnapshot[] {
  const chain: ActivatedRouteSnapshot[] = [];
  let route: ActivatedRouteSnapshot | undefined = root;
  while (route !== undefined) {
    chain.push(route);
    route = route.children.find((child) => child.outlet === PRIMARY_OUTLET);
  }
  return chain;
}

// The status an outcome page shows; null for a redirect, which the browser follows instead.
function errorStatusOf(outcome: PageOutcome): number | null {
  switch (outcome.kind) {
    case 'redirect':
      return null;
    case 'not-found':
      return HttpStatusCode.NotFound;
    default:
      return outcome.status;
  }
}

// The path and the query of a router URL; its fragment never leaves the page.
function splitUrl(url: string): { path: string; search: string } {
  const [beforeFragment] = url.split(FRAGMENT_START, 1);
  const start = beforeFragment.indexOf(QUERY_START);
  return start === -1
    ? { path: beforeFragment, search: '' }
    : { path: beforeFragment.slice(0, start), search: beforeFragment.slice(start) };
}

// The pinned version of the path, else a well-formed `?version=` (the home reads it in place).
function versionOf(leaf: ActivatedRouteSnapshot, keptQuery: string): string {
  const inPath = leaf.params[VERSION_PARAM] as string | undefined;
  return inPath ?? QueryString.parse(keptQuery).get(VERSION_PARAM) ?? CURRENT_VERSION;
}

/**
 * A navigation as the audience receives it, refused by default: the template of its routes,
 * except a route marked `audience: 'path'` (lists and details), sent as its real path with
 * the whitelisted query; never a fragment. Null for what is never sent: an account or admin
 * page, or the outcome of a redirect.
 */
export function audiencePageOf(root: ActivatedRouteSnapshot, url: string): AudiencePage | null {
  const chain = chainOf(root);
  const leaf = chain[chain.length - 1];
  const mode = leaf.data[AUDIENCE_ROUTE_DATA.key];
  const outcome = leaf.data[OUTCOME_KEY] as PageOutcome | undefined;
  const errorStatus = outcome === undefined ? null : errorStatusOf(outcome);
  if (mode === AUDIENCE_ROUTE_DATA.none.audience || (outcome !== undefined && !errorStatus)) {
    return null;
  }
  const { path, search } = splitUrl(url);
  const keptQuery = audienceQueryOf(search);
  const isPublicPath = mode === AUDIENCE_ROUTE_DATA.path.audience && errorStatus === null;
  return {
    key: pageKeyOf(path + search),
    url: isPublicPath ? withHomeSlash(path) + keptQuery : audienceTemplateOf(chain),
    isPublicPath,
    errorStatus,
    locale: (leaf.params[LOCALE_PARAM] as string | undefined) ?? null,
    version: versionOf(leaf, keptQuery),
    isSearching: !!QueryString.parse(search).get(SEARCH_PARAM),
  };
}
