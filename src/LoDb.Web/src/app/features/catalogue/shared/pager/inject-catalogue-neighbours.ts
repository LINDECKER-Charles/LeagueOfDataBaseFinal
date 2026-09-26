import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, type Signal, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import type { ResourceType } from '../../../../core/api/generated/models/resource-type';
import { CatalogueLists } from '../data/catalogue-lists';
import type { ListByResource } from '../data/list-by-resource';
import type { NeighbourQuery } from './neighbour-query';
import type { Neighbours } from './neighbours';
import { neighboursOf } from './neighbours-of';

type CardOf<R extends ResourceType> = ListByResource[R]['entries'][number];

/**
 * The previous and next entries of a detail page, for lodb-pager. They are read from the
 * whole list, fetched in the browser only: rendering it on the server would ship the list
 * in the transfer state of every detail page. The list is fetched once per version and
 * language, so turning pages costs no request.
 */
export function injectCatalogueNeighbours<R extends ResourceType>(
  resource: R,
  at: Signal<NeighbourQuery | undefined>,
  keyOf: (card: CardOf<R>) => string,
): Signal<Neighbours<CardOf<R>>> {
  const lists = inject(CatalogueLists);
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  const version = computed(() => at()?.context.version);
  const lang = computed(() => at()?.context.language);
  const list = rxResource({
    params: () => {
      const [v, l] = [version(), lang()];
      return isBrowser && v !== undefined && l !== undefined ? { version: v, lang: l } : undefined;
    },
    stream: ({ params }) =>
      lists
        .fetch(resource, params)
        .pipe(map((outcome) => (outcome.kind === 'list' ? outcome.list.entries : []))),
  });
  return computed(() => {
    const key = at()?.key;
    const cards: readonly CardOf<R>[] = list.value() ?? [];
    return key === undefined ? { previous: null, next: null } : neighboursOf(cards, key, keyOf);
  });
}
