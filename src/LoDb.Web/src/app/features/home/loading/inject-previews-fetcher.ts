import { inject } from '@angular/core';
import { catchError, firstValueFrom, forkJoin, map, type Observable, of } from 'rxjs';
import { CatalogService } from '../../../core/api/generated/services/catalog.service';
import type { PageContext } from '../../../core/context/page-context';
import type { Preview } from '../data/preview';
import type { Previews } from '../data/previews';
import { PREVIEW_OF } from './preview-of';
import { PREVIEW_SIZE } from './preview-size';

const FIRST_PAGE = 1;

// A failing list leaves its section empty instead of taking the whole home down, as the
// legacy home did: the three others still answer.
function isolated(preview: Observable<Preview>): Observable<Preview | null> {
  return preview.pipe(catchError(() => of(null)));
}

/**
 * Builds the fetcher of the four previews, injected up front since the resolver calls it
 * after an await. The four first pages are requested together.
 */
export function injectPreviewsFetcher(): (context: PageContext) => Promise<Previews> {
  const catalog = inject(CatalogService);
  return (context) => {
    const params = {
      version: context.version,
      lang: context.language,
      page: FIRST_PAGE,
      size: PREVIEW_SIZE,
    };
    return firstValueFrom(
      forkJoin({
        champions: isolated(catalog.listChampions(params).pipe(map(PREVIEW_OF.champions))),
        items: isolated(catalog.listItems(params).pipe(map(PREVIEW_OF.items))),
        runes: isolated(catalog.listRunes(params).pipe(map(PREVIEW_OF.runes))),
        summoners: isolated(catalog.listSummoners(params).pipe(map(PREVIEW_OF.summoners))),
      }),
    );
  };
}
