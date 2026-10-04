import type { ParamMap } from '@angular/router';
import type { TrendsQuery } from './trends-query';

const WHOLE_NUMBER = /^\d+$/;

function filterOf(params: ParamMap, name: string): string | null {
  const value = params.get(name)?.trim();
  return value ? value : null;
}

// A page that is not a whole number from 1 reads as the first, as the API does.
function pageOf(raw: string | null): number {
  const page = raw !== null && WHOLE_NUMBER.test(raw) ? Number(raw) : 1;
  return Number.isSafeInteger(page) && page >= 1 ? page : 1;
}

/** The trends a URL asks for, read from its query. */
export function trendsQueryOf(params: ParamMap): TrendsQuery {
  return {
    champion: filterOf(params, 'champion'),
    mode: filterOf(params, 'mode'),
    language: filterOf(params, 'language'),
    page: pageOf(params.get('page')),
  };
}
