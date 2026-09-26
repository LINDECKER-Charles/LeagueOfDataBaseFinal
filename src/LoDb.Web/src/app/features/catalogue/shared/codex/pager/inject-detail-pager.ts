import { type Signal, computed } from '@angular/core';
import type { ResourceType } from '../../../../../core/api/generated/models/resource-type';
import type { PagerLink } from '../../../../../ui/navigation/pager-link';
import type { ListByResource } from '../../data/list-by-resource';
import { injectCatalogueNeighbours } from '../../pager/inject-catalogue-neighbours';
import type { NeighbourQuery } from '../../pager/neighbour-query';
import { injectCatalogueLink } from '../links/inject-catalogue-link';
import type { DetailPagerLinks } from './detail-pager-links';

type CardOf<R extends ResourceType> = ListByResource[R]['entries'][number];

/** Which list a detail page turns the pages of, and how its cards are told apart. */
export interface DetailPagerSource<R extends ResourceType> {
  readonly resource: R;
  /** The page's context and the key of its entry. */
  readonly at: () => NeighbourQuery;
  readonly keyOf: (card: CardOf<R>) => string;
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
  const linkTo = injectCatalogueLink();
  return computed(() => {
    const { context } = at();
    const { previous, next } = neighbours();
    const linkOf = (card: { name: string; canonicalPath: string }): PagerLink => ({
      url: linkTo(context, card.canonicalPath),
      name: card.name,
    });
    return {
      previous: previous && linkOf(previous),
      next: next && linkOf(next),
      hub: linkTo(context, source.resource),
    };
  });
}
