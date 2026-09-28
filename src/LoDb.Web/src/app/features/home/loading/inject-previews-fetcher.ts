import { inject } from '@angular/core';
import { catchError, firstValueFrom, forkJoin, map, type Observable, of } from 'rxjs';
import type { ResourceType } from '../../../core/api/generated/models/resource-type';
import { CatalogService } from '../../../core/api/generated/services/catalog.service';
import type { StrictHttpResponse } from '../../../core/api/generated/strict-http-response';
import type { PageContext } from '../../../core/context/page-context';
import { retryAfterOf } from '../../../core/http/retry-after-of';
import type { FetchedPreviews } from '../data/fetched-previews';
import type { Preview } from '../data/preview';
import { injectRetryTransfer } from './inject-retry-transfer';
import { PREVIEW_OF } from './preview-of';
import { PREVIEW_SIZE } from './preview-size';

const FIRST_PAGE = 1;

interface Read {
  readonly preview: Preview | null;
  readonly retryAfterMs: number | null;
}

function isPending(preview: Preview): boolean {
  return preview.entries.some((entry) => entry.image.status === 'pending');
}

// A failing list leaves its section empty instead of taking the whole home down, as the
// legacy home did: the three others still answer. A list says when to read it again
// (`Retry-After`) while any image of its version is pending: only a preview showing one
// is worth that second read.
function read<L>(
  response: Observable<StrictHttpResponse<L>>,
  previewOf: (list: L) => Preview,
): Observable<Read> {
  return response.pipe(
    map((answer) => {
      const preview = previewOf(answer.body);
      const delay = retryAfterOf(answer.headers.get('Retry-After'), Date.now());
      return { preview, retryAfterMs: isPending(preview) ? delay : null };
    }),
    catchError(() => of({ preview: null, retryAfterMs: null })),
  );
}

function fetchedOf(reads: Readonly<Record<ResourceType, Read>>): FetchedPreviews {
  const delays = Object.values(reads).flatMap((one) => one.retryAfterMs ?? []);
  return {
    previews: {
      champions: reads.champions.preview,
      items: reads.items.preview,
      runes: reads.runes.preview,
      summoners: reads.summoners.preview,
    },
    retryAfterMs: delays.length === 0 ? null : Math.max(...delays),
  };
}

/**
 * Builds the fetcher of the four previews, injected up front since the resolver calls it
 * after an await. The four first pages are requested together.
 */
export function injectPreviewsFetcher(): (context: PageContext) => Promise<FetchedPreviews> {
  const catalog = inject(CatalogService);
  const carry = injectRetryTransfer();
  return (context) => {
    const params = {
      version: context.version,
      lang: context.language,
      page: FIRST_PAGE,
      size: PREVIEW_SIZE,
    };
    return firstValueFrom(
      forkJoin({
        champions: read(catalog.listChampions$Response(params), PREVIEW_OF.champions),
        items: read(catalog.listItems$Response(params), PREVIEW_OF.items),
        runes: read(catalog.listRunes$Response(params), PREVIEW_OF.runes),
        summoners: read(catalog.listSummoners$Response(params), PREVIEW_OF.summoners),
      }).pipe(
        map(fetchedOf),
        map((fetched) => ({ ...fetched, retryAfterMs: carry(fetched.retryAfterMs) })),
      ),
    );
  };
}
