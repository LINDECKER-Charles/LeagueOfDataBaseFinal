import { QueryString } from '../../../../core/routing/url/query-string';
import type { FacetDefinition } from '../facets/model/facet-definition';
import type { FacetSelection } from '../facets/model/facet-selection';
import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import { FILTER_URL_PARAMS as PARAMS } from './filter-url-params';
import type { FilterUrlSpec } from './filter-url-spec';
import type { FilterUrlState } from './filter-url-state';

const FIRST_PAGE = 1;

function positiveInt(raw: string | null): number | undefined {
  const value = Number(raw);
  return raw !== null && Number.isInteger(value) && value > 0 ? value : undefined;
}

function sizeOf(raw: string | null): number | undefined {
  return raw === PARAMS.allSizes ? PAGE_SIZE_ALL : positiveInt(raw);
}

function choiceOf(
  query: QueryString,
  facet: FacetDefinition,
  raw: string,
): FacetSelection | undefined {
  const values = [...new Set(raw.split(PARAMS.listSeparator).filter(Boolean))];
  if (values.length === 0) {
    return undefined;
  }
  const all = facet.matchAll && query.get(facet.key + PARAMS.matchAllSuffix) === PARAMS.flag;
  return { values: facet.multiple ? values : values.slice(0, 1), all };
}

function rangeOf(raw: string): FacetSelection | undefined {
  const [min, max] = raw.split(PARAMS.rangeSeparator, 2).map(Number);
  const isValid = Number.isFinite(min) && Number.isFinite(max) && min <= max;
  return isValid ? { min, max } : undefined;
}

function selectionOf(query: QueryString, facet: FacetDefinition): FacetSelection | undefined {
  const raw = query.get(facet.key);
  if (raw === null || raw === '') {
    return undefined;
  }
  switch (facet.kind) {
    case 'choice':
      return choiceOf(query, facet, raw);
    case 'range':
      return rangeOf(raw);
    case 'toggle':
      return raw === PARAMS.flag ? true : undefined;
  }
}

/**
 * The list state a URL carries, leniently: a malformed or unknown parameter falls back to
 * the default rather than failing, an inverted range is dropped, a single choice keeps its
 * first value, and a match-all flag counts only on a facet that offers it.
 */
export function parseFilterUrl(search: string, spec: FilterUrlSpec): FilterUrlState {
  const query = QueryString.parse(search);
  const facets: Record<string, FacetSelection> = {};
  for (const facet of spec.schema) {
    const selection = selectionOf(query, facet);
    if (selection !== undefined) {
      facets[facet.key] = selection;
    }
  }
  return {
    query: query.get(PARAMS.query) ?? '',
    facets,
    page: positiveInt(query.get(PARAMS.page)) ?? FIRST_PAGE,
    size: sizeOf(query.get(PARAMS.size)) ?? spec.defaultSize,
  };
}
