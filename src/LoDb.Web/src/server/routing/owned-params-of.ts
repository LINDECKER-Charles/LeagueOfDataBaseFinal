import type { ResourceType } from '../../app/core/api/generated/models/resource-type';
import { isCatalogueRoute } from '../../app/core/routing/url/is-catalogue-route';
import { PATCH_CHANGES_PATH } from '../../app/core/routing/patch-changes-path';
import { CHAMPION_FACET_KEYS } from '../../app/features/catalogue/champions/list/champion-facet-keys';
import { ITEM_FACET_KEYS } from '../../app/features/catalogue/items/list/facets/item-facet-keys';
import { RUNE_FACET_KEYS } from '../../app/features/catalogue/runes/list/rune-facet-keys';
import { FILTER_URL_PARAMS } from '../../app/features/catalogue/shared/url/filter-url-params';
import { SUMMONER_FACET_KEYS } from '../../app/features/catalogue/summoners/list/summoner-facet-keys';

type FacetKeys = Readonly<Record<string, string | readonly string[]>>;

// Every catalogue page takes its version and its language variant from the query too
// (`core/context/page-context-of.ts`); a `?version=` then moves into the path (canonical.ts).
const PAGE_PARAMS = ['version', 'lang'];
const LIST_PARAMS = [
  ...PAGE_PARAMS,
  FILTER_URL_PARAMS.query,
  FILTER_URL_PARAMS.page,
  FILTER_URL_PARAMS.size,
];
const SEGMENTS_OF_LIST = 1;
const SEGMENTS_OF_DETAIL = 2;

// A facet is read under its key, and its any/all switch under `<key>_all`.
function listParamsOf(keys: FacetKeys): ReadonlySet<string> {
  const facets = Object.values(keys).flat();
  const switches = facets.map((key) => key + FILTER_URL_PARAMS.matchAllSuffix);
  return new Set([...LIST_PARAMS, ...facets, ...switches]);
}

const LIST_PARAMS_BY_RESOURCE: Readonly<Record<ResourceType, ReadonlySet<string>>> = {
  champions: listParamsOf(CHAMPION_FACET_KEYS),
  items: listParamsOf(ITEM_FACET_KEYS),
  runes: listParamsOf(RUNE_FACET_KEYS),
  summoners: listParamsOf(SUMMONER_FACET_KEYS),
};
const DETAIL_PARAMS: ReadonlySet<string> = new Set(PAGE_PARAMS);
// The history of the patches reads no query: one URL, one cached copy per locale.
const PATCH_HISTORY_PAGE = 'patches';
const NO_PARAMS: ReadonlySet<string> = new Set();

function isOnly(page: readonly string[], segment: string): boolean {
  return page.length === 1 && page[0] === segment;
}

/**
 * The query parameters a page of the catalogue reads, from its decoded segments below the
 * locale and the version: a list its search, paging and facets (ADR 0005), a detail and the
 * page of a patch's changes none of their own, the history of the patches none at all. Null
 * for any other page, whose query the server leaves to it.
 */
export function ownedParamsOf(page: readonly string[]): ReadonlySet<string> | null {
  if (isOnly(page, PATCH_HISTORY_PAGE)) {
    return NO_PARAMS;
  }
  if (isOnly(page, PATCH_CHANGES_PATH)) {
    return DETAIL_PARAMS;
  }
  if (!isCatalogueRoute(page)) {
    return null;
  }
  if (page.length === SEGMENTS_OF_LIST) {
    return LIST_PARAMS_BY_RESOURCE[page[0] as ResourceType];
  }
  return page.length === SEGMENTS_OF_DETAIL ? DETAIL_PARAMS : null;
}
