import { type Signal, computed } from '@angular/core';
import type { ResourceType } from '../../../../../core/api/generated/models/resource-type';
import type { PageContext } from '../../../../../core/context/page-context';
import type { PagerLink } from '../../../../../ui/navigation/pager-link';
import type { ListByResource } from '../../../shared/data/list-by-resource';
import { catalogueHref } from '../../../shared/links/catalogue-href';
import { injectCatalogueNeighbours } from '../../../shared/pager/inject-catalogue-neighbours';
import type { NeighbourQuery } from '../../../shared/pager/neighbour-query';
import type { DetailPagerLinks } from './detail-pager-links';

type CardOf<R extends ResourceType> = ListByResource[R]['entries'][number];

/** Which list a detail page turns the pages of, and how its cards are told apart. */
export interface DetailPagerSource<R extends ResourceType> {
  readonly resource: R;
  /** The page's context and the key of its entry. */
  readonly at: () => NeighbourQuery;
  readonly keyOf: (card: CardOf<R>) => string;
}

// Without `?lang=`: lodb-pager takes its links as strings, which the router would escape.
function linkOf(context: PageContext, card: { name: string; canonicalPath: string }): PagerLink {
  return { url: catalogueHref(context, card.canonicalPath), name: card.name };
}

/**
 * The previous and next entries of a detail page in its list's order, and the list itself,
 * for lodb-pager. The neighbours arrive in the browser only (injectCatalogueNeighbours).
 */
export function injectDetailPager<R extends ResourceType>(
  source: DetailPagerSource<R>,
): Signal<DetailPagerLinks> {
  const at = computed(source.at);
  const neighbours = injectCatalogueNeighbours(source.resource, at, source.keyOf);
  return computed(() => {
    const { context } = at();
    const { previous, next } = neighbours();
    return {
      previous: previous && linkOf(context, previous),
      next: next && linkOf(context, next),
      hub: catalogueHref(context, source.resource),
    };
  });
}
