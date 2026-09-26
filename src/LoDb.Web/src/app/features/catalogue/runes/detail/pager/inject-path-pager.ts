import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, type Signal, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import type { RuneTreeCard } from '../../../../../core/api/generated/models/rune-tree-card';
import type { PageContext } from '../../../../../core/context/page-context';
import type { PagerLink } from '../../../../../ui/navigation/pager-link';
import type { DetailPagerLinks } from '../../../shared/codex/pager/detail-pager-links';
import { injectCatalogueLink } from '../../../shared/codex/links/inject-catalogue-link';
import { CatalogueLists } from '../../../shared/data/catalogue-lists';
import { neighboursOf } from '../../../shared/pager/neighbours-of';

// Every rune list answers its paths whatever the page: the smallest page is enough.
const SMALLEST_PAGE = { page: 1, size: 1 };

/**
 * The previous and next rune paths of a path page, for lodb-pager. Its neighbours are paths,
 * not the runes the list's entries are, so they are read from the list's `trees`. Fetched in
 * the browser only, as injectCatalogueNeighbours does, not to weigh on the server render.
 */
export function injectPathPager(
  at: () => { readonly context: PageContext; readonly key: string },
): Signal<DetailPagerLinks> {
  const lists = inject(CatalogueLists);
  const linkTo = injectCatalogueLink();
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
    const linkOf = (tree: RuneTreeCard | null): PagerLink | null =>
      tree && { url: linkTo(context, tree.canonicalPath), name: tree.name };
    return {
      previous: linkOf(previous),
      next: linkOf(next),
      hub: linkTo(context, 'runes'),
    };
  });
}
