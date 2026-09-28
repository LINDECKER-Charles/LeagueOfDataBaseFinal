import type { Params } from '@angular/router';
import type { TrendsFilters } from './trends-filters';

function paramOf(value: string | null): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

/**
 * The query a change of filters navigates to, merged into the current one: a blank filter
 * leaves the URL (null removes a parameter), and the list starts again at its first page.
 * `?version=` and `?lang=`, which name the builds' names, are kept by the merge.
 */
export function filterParamsOf(filters: TrendsFilters): Params {
  return {
    champion: paramOf(filters.champion),
    mode: paramOf(filters.mode),
    language: paramOf(filters.language),
    page: null,
  };
}

/** The query of another page of the same filters: the first page carries none. */
export function pageParamsOf(page: number): Params {
  return { page: page > 1 ? page : null };
}
