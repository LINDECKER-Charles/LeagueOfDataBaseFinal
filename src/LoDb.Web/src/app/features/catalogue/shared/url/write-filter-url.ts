import { QueryString } from '../../../../core/routing/url/query-string';
import type { FacetDefinition } from '../facets/model/facet-definition';
import type { FacetSelection } from '../facets/model/facet-selection';
import { isChoiceSelection } from '../facets/rules/is-choice-selection';
import { isRangeSelection } from '../facets/rules/is-range-selection';
import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import { FILTER_URL_PARAMS as PARAMS } from './filter-url-params';
import type { FilterUrlSpec } from './filter-url-spec';
import type { FilterUrlState } from './filter-url-state';

const FIRST_PAGE = 1;
/** Largest meaningful precision of a facet value (attack speed: 0.625). */
const RANGE_DECIMALS = 3;

function formatNumber(value: number): string {
  return String(Number(value.toFixed(RANGE_DECIMALS)));
}

function withParam(
  query: QueryString,
  facet: FacetDefinition,
  selection?: FacetSelection,
): QueryString {
  if (selection === undefined) {
    return query;
  }
  if (selection === true) {
    return query.with(facet.key, PARAMS.flag);
  }
  if (isRangeSelection(selection)) {
    const bounds = [selection.min, selection.max].map(formatNumber);
    return query.with(facet.key, bounds.join(PARAMS.rangeSeparator));
  }
  if (!isChoiceSelection(selection) || selection.values.length === 0) {
    return query;
  }
  const chosen = query.with(facet.key, [...selection.values].sort().join(PARAMS.listSeparator));
  const isAll = selection.all && facet.matchAll;
  return isAll ? chosen.with(facet.key + PARAMS.matchAllSuffix, PARAMS.flag) : chosen;
}

function ownedNames(schema: readonly FacetDefinition[]): string[] {
  const facets = schema.flatMap((facet) => [facet.key, facet.key + PARAMS.matchAllSuffix]);
  return [PARAMS.query, PARAMS.page, PARAMS.size, ...facets];
}

/**
 * The query string of a list state, canonical so one state has one URL: schema order,
 * sorted values, defaults omitted, both range bounds written. The parameters the list does
 * not own (`lang`, `version`...) come first, byte for byte as they were.
 */
export function writeFilterUrl(search: string, state: FilterUrlState, spec: FilterUrlSpec): string {
  let query = QueryString.parse(search).without(...ownedNames(spec.schema));
  const text = state.query.trim();
  if (text !== '') {
    query = query.with(PARAMS.query, text);
  }
  for (const facet of spec.schema) {
    query = withParam(query, facet, state.facets[facet.key]);
  }
  if (state.page > FIRST_PAGE) {
    query = query.with(PARAMS.page, String(state.page));
  }
  if (state.size !== spec.defaultSize) {
    const size = state.size === PAGE_SIZE_ALL ? PARAMS.allSizes : String(state.size);
    query = query.with(PARAMS.size, size);
  }
  return query.toString();
}
