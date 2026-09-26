import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, type Signal, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import type { RuneTreeCard } from '../../../../../core/api/generated/models/rune-tree-card';
import type { PageContext } from '../../../../../core/context/page-context';
import type { PagerLink } from '../../../../../ui/navigation/pager-link';
import type { DetailPagerLinks } from '../../../items/codex/pager/detail-pager-links';
import { CatalogueLists } from '../../../shared/data/catalogue-lists';
import { catalogueHref } from '../../../shared/links/catalogue-href';
import { neighboursOf } from '../../../shared/pager/neighbours-of';

// Every rune list answers its paths whatever the page: the smallest page is enough.
const SMALLEST_PAGE = { page: 1, size: 1 };

// Without `?lang=`: lodb-pager takes its links as strings, which the router would escape.
function linkOf(context: PageContext, tree: RuneTreeCard | null): PagerLink | null {
  return tree && { url: catalogueHref(context, tree.canonicalPath), name: tree.name };
}

/**
 * The previous and next rune paths of a path page, for lodb-pager. Its neighbours are paths,
 * not the runes the list's entries are, so they are read from the list's `trees`. Fetched in
 * the browser only, as injectCatalogueNeighbours does, not to weigh on the server render.
 */
export function injectPathPager(
  at: () => { readonly context: PageContext; readonly key: string },
): Signal<DetailPagerLinks> {
  const lists = inject(CatalogueLists);
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  const where = computed(at);
  const trees = rxResource({
    params: () => {
      const { version, language } = where().context;
      return isBrowser ? { version, lang: language, ...SMALLEST_PAGE } : undefined;
    },
    stream: ({ params }) =>
      lists
        .fetch('runes', params)
        .pipe(map((outcome) => (outcome.kind === 'list' ? outcome.list.trees : []))),
  });
  return computed(() => {
    const { context, key } = where();
    const { previous, next } = neighboursOf(trees.value() ?? [], key, (tree) => tree.key);
    return {
      previous: linkOf(context, previous),
      next: linkOf(context, next),
      hub: catalogueHref(context, 'runes'),
    };
  });
}
