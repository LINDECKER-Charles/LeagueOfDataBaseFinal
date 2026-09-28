import { HttpStatusCode } from '@angular/common/http';
import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import type { PageContext } from '../../context/page-context';
import { pageContextOf } from '../../context/page-context-of';
import { canonical } from '../canonical';
import type { CataloguePage } from '../catalogue-page';
import type { PageOutcome } from '../outcome/page-outcome';
import { factsOf } from './facts-of';

// Nothing ingested yet: the catalogue exists, it cannot be read for now.
const NOT_INGESTED: PageOutcome = {
  kind: 'failure',
  status: HttpStatusCode.ServiceUnavailable,
  retryAfter: null,
};

/**
 * Opens a catalogue page from `/api/meta`: the canonical outcome that replaces it (unknown
 * version, latest version pinned, `?version=` to move into the path), or its context. The
 * server reads no cookie, so neither does this: the first browser render matches.
 */
export function openCatalogue(page: CataloguePage, meta: CatalogMeta): PageContext | PageOutcome {
  const outcome = canonical(page, factsOf(meta));
  if (outcome !== null) {
    return outcome;
  }
  const sources = { locale: page.locale, path: page.pinned, query: page.query, remembered: null };
  return pageContextOf(sources, meta) ?? NOT_INGESTED;
}
