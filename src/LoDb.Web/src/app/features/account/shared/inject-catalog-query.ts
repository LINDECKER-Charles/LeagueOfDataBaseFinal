import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiMeta } from '../../../core/api/meta/api-meta';
import { pageContextOf } from '../../../core/context/page-context-of';
import { PreferencesStore } from '../../../core/context/preferences/preferences-store';
import { PageDirection } from '../../../core/layout/direction/page-direction';

/** The version and the Data Dragon language a profile call resolves its catalogue on. */
export interface CatalogQuery {
  readonly version?: string;
  readonly lang?: string;
}

const QUERY_START = '?';
const FRAGMENT_START = '#';

// The query of the URL the router serialized, `?` included, or ''. The home has its copy;
// a feature cannot share it.
function queryOf(url: string): string {
  const path = url.split(FRAGMENT_START, 1)[0];
  const start = path.indexOf(QUERY_START);
  return start < 0 ? '' : path.slice(start);
}

/**
 * The context the private profile pages read the catalogue in: the version and the language
 * of the URL, then the remembered ones, then the defaults (ADR 0005). Rendered in the browser
 * only, these pages may read the remembered context at once. Nothing is asked for while no
 * version is ingested: the API then answers with its own defaults.
 */
export function injectCatalogQuery(): () => Promise<CatalogQuery> {
  const meta = inject(ApiMeta);
  const preferences = inject(PreferencesStore);
  const router = inject(Router);
  const page = inject(PageDirection);
  return async () => {
    const sources = {
      locale: page.locale(),
      path: null,
      query: queryOf(router.url),
      remembered: preferences.read(),
    };
    const context = pageContextOf(sources, await firstValueFrom(meta.meta()));
    return context === null ? {} : { version: context.version, lang: context.language };
  };
}
