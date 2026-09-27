import { inject, type Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, type ParamMap, type Params, Router } from '@angular/router';
import { ADMIN_RANGES, type AdminRange } from './admin-range';

/** The query of an admin page: its filters and its page, kept in the URL. */
export interface AdminQuery {
  readonly params: Signal<ParamMap>;
  /** The text of parameter `name`, '' when absent. */
  text(name: string): string;
  /** The page asked for, 1 unless a positive integer. */
  page(): number;
  /** The period of an analytics panel, 30 days unless one of the known ones. */
  range(): AdminRange;
  /** Merges `params` into the query (null removes one) and goes back to the first page. */
  set(params: Params): void;
}

const PAGE = 'page';
const RANGE = 'range';
const DEFAULT_RANGE: AdminRange = '30d';

function isRange(value: string | null): value is AdminRange {
  return (ADMIN_RANGES as readonly (string | null)[]).includes(value);
}

/**
 * The query of the current admin page, read and written through the URL, so that the
 * history and a shared link come back to the same list.
 */
export function injectQuery(): AdminQuery {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const params = toSignal(route.queryParamMap, { requireSync: true });
  return {
    params,
    text: (name) => params().get(name) ?? '',
    page: () => {
      const page = Number(params().get(PAGE));
      return Number.isInteger(page) && page > 0 ? page : 1;
    },
    range: () => {
      const range = params().get(RANGE);
      return isRange(range) ? range : DEFAULT_RANGE;
    },
    set: (changes) =>
      void router.navigate([], {
        relativeTo: route,
        queryParams: { [PAGE]: null, ...changes },
        queryParamsHandling: 'merge',
      }),
  };
}
