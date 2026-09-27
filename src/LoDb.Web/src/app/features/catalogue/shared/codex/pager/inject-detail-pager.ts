import { type Signal, computed } from '@angular/core';
import type { DetailNeighbour } from '../../../../../core/api/generated/models/detail-neighbour';
import type { DetailNeighbours } from '../../../../../core/api/generated/models/detail-neighbours';
import type { ResourceType } from '../../../../../core/api/generated/models/resource-type';
import type { PageContext } from '../../../../../core/context/page-context';
import type { PagerLink } from '../../../../../ui/navigation/pager-link';
import { injectCatalogueLink } from '../links/inject-catalogue-link';
import { injectTranslate } from '../texts/inject-translate';
import type { DetailPagerLinks } from './detail-pager-links';

/** Where a detail page stands: the list it belongs to, and its neighbours in that list. */
export interface DetailPagerSource {
  readonly resource: ResourceType;
  readonly context: PageContext;
  /** The entries on either side, in the list's order, as the detail payload carries them. */
  readonly neighbours: DetailNeighbours;
}

/**
 * The previous and next entries of a detail page, and the list itself, for lodb-pager. The
 * neighbours come with the page's own payload, so the server renders them, as the legacy
 * pager did. A LoL Classic neighbour is marked: its name alone repeats a current entry's.
 */
export function injectDetailPager(source: () => DetailPagerSource): Signal<DetailPagerLinks> {
  const linkTo = injectCatalogueLink();
  const translate = injectTranslate();
  return computed(() => {
    const { resource, context, neighbours } = source();
    const t = translate();
    const classic = { label: t('edition.classic'), hint: t('edition.classic_hint') };
    const linkOf = (neighbour: DetailNeighbour | null): PagerLink | null => {
      if (neighbour === null) {
        return null;
      }
      const link = { url: linkTo(context, neighbour.canonicalPath), name: neighbour.name };
      return neighbour.edition === 'classic' ? { ...link, mark: classic } : link;
    };
    return {
      previous: linkOf(neighbours.previous),
      next: linkOf(neighbours.next),
      hub: linkTo(context, resource),
    };
  });
}
