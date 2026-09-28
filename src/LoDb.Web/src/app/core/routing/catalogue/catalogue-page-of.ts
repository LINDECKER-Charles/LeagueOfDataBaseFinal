import type { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import type { ResourceType } from '../../api/generated/models/resource-type';
import { DEFAULT_LOCALE } from '../../i18n/default-locale';
import { isLocale } from '../../i18n/is-locale';
import type { CataloguePage } from '../catalogue-page';

const QUERY_START = '?';
const FRAGMENT_START = '#';

// The query as the router serialized it, `?` included: what every redirect carries on.
function queryOf(url: string): string {
  const path = url.split(FRAGMENT_START, 1)[0];
  const start = path.indexOf(QUERY_START);
  return start < 0 ? '' : path.slice(start);
}

/**
 * The catalogue page a route designates. Reads `locale`, `version` and `id` from the
 * parameters, inherited from the parent routes (`paramsInheritanceStrategy: 'always'`).
 */
export function cataloguePageOf(
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot,
  resource: ResourceType,
): CataloguePage {
  const locale = route.paramMap.get('locale');
  return {
    locale: isLocale(locale) ? locale : DEFAULT_LOCALE,
    pinned: route.paramMap.get('version'),
    resource,
    entry: route.paramMap.get('id'),
    query: queryOf(state.url),
  };
}
