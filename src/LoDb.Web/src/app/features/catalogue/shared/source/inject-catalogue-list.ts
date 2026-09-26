import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, type Signal, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { type Observable, tap } from 'rxjs';
import type { ResourceType } from '../../../../core/api/generated/models/resource-type';
import type { PageContext } from '../../../../core/context/page-context';
import { PageResponse } from '../../../../core/routing/response/page-response';
import { CatalogueLists } from '../data/catalogue-lists';
import type { ListByResource } from '../data/list-by-resource';
import type { ListOutcome } from '../data/list-outcome';
import { pageRequestOf } from '../data/page-request-of';
import { withOneRetry } from '../data/with-one-retry';
import { parseFilterUrl } from '../url/parse-filter-url';
import { searchOf } from '../url/search-of';
import type { CatalogueListSource } from './catalogue-list-source';
import { DEFAULT_PAGE_SIZE } from './default-page-size';
import { scopedFetch } from './scoped-fetch';
import { statusOf } from './status-of';

interface Scope {
  readonly version: string;
  readonly lang: string;
}

type Fetch<L> = (page: { page?: number; size?: number }, scope: Scope) => Observable<L>;

function sameScope(a: Scope | undefined, b: Scope | undefined): boolean {
  return a?.version === b?.version && a?.lang === b?.lang;
}

function listOf<L>(outcome: ListOutcome<L> | undefined): L | null {
  return outcome?.kind === 'list' ? outcome.list : null;
}

// A render with placeholders, or without its list, must not be kept like a complete one.
function markTransient<L>(outcome: ListOutcome<L>, response: PageResponse): void {
  if (outcome.kind !== 'list' || outcome.retryAfterMs !== null) {
    response.cache('transient');
  }
}

// The page the URL names, as the opening request asks for it.
function openingOf(url: string, defaultSize: number) {
  const { page, size } = parseFilterUrl(searchOf(url), { schema: [], defaultSize });
  const request = pageRequestOf(page, size);
  return { slice: { page, size }, request, isWhole: request.page === undefined };
}

function scopeOf(context: Signal<PageContext | undefined>): Signal<Scope | undefined> {
  return computed<Scope | undefined>(
    () => {
      const current = context();
      return current && { version: current.version, lang: current.language };
    },
    { equal: sameScope },
  );
}

/**
 * The list of a catalogue page, to call from a page's field initializer:
 * `protected readonly list = injectCatalogueList('items', this.context);`.
 *
 * The server fetches the page the URL names, never waits for a retry, and lets the proxy keep
 * a render with placeholders a minute only. The browser takes that page back from the
 * transfer cache, then fetches the whole list, retried once after its `Retry-After` while
 * the version is cold. Both refetch when the version or the language changes, never on a
 * mere query change such as the filter's own.
 */
export function injectCatalogueList<R extends ResourceType>(
  resource: R,
  context: Signal<PageContext | undefined>,
  defaultSize = DEFAULT_PAGE_SIZE,
): CatalogueListSource<ListByResource[R]> {
  type Outcome = ListOutcome<ListByResource[R]>;
  const lists = inject(CatalogueLists);
  const response = inject(PageResponse);
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  const { slice, request: opening, isWhole } = openingOf(inject(Router).url, defaultSize);
  const scope = scopeOf(context);
  const fetch: Fetch<Outcome> = (slice, at) => lists.fetch(resource, { ...at, ...slice });
  const onServer = (at: Scope) =>
    fetch(opening, at).pipe(tap((outcome) => markTransient(outcome, response)));
  const inBrowser = (at: Scope) =>
    isWhole ? withOneRetry(() => fetch(opening, at)) : fetch(opening, at);
  const first = scopedFetch(scope, (at) => (isBrowser ? inBrowser(at) : onServer(at)));
  const whole = rxResource({
    params: () => (isBrowser && !isWhole ? scope() : undefined),
    stream: ({ params }) => withOneRetry(() => fetch({}, params)),
  });
  const firstPage = computed(() => listOf(first()));
  const dataset = computed(() => (isBrowser && isWhole ? firstPage() : listOf(whole.value())));
  const list = computed(() => dataset() ?? firstPage());
  const status = computed(() => statusOf(whole.value() ?? first()));
  return { firstPage, slice, defaultSize, dataset, list, status };
}
