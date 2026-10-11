import type { Provider } from '@angular/core';
import { of } from 'rxjs';
import type { CatalogMeta } from '../../../../core/api/generated/models/catalog-meta';
import { ApiMeta } from '../../../../core/api/meta/api-meta';

/**
 * `/api/meta` as a detail page's blocks read it (the previous patches), without HTTP: only
 * the latest version and the listed ones matter to them. None listed by default.
 */
export function provideMeta(
  meta: Pick<CatalogMeta, 'latest' | 'versions'> = { latest: null, versions: [] },
): Provider {
  return { provide: ApiMeta, useValue: { meta: () => of(meta) } };
}
