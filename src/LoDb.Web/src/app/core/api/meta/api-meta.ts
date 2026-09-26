import { inject, Injectable } from '@angular/core';
import { map, type Observable, shareReplay } from 'rxjs';
import type { CatalogMeta } from '../generated/models/catalog-meta';
import type { UiLocale } from '../generated/models/ui-locale';
import { MetaService } from '../generated/services/meta.service';
import { languageOf } from './language-of';
import { versionMatcher } from './version-matcher';

/**
 * `/api/meta`, requested once per application (once per request on the server) and shared:
 * the locale → language mapping and the version pattern come from it, never from a copy
 * written by hand. A failed request is not cached: the next subscriber asks again.
 */
@Injectable({ providedIn: 'root' })
export class ApiMeta {
  private readonly meta$ = inject(MetaService)
    .getMeta()
    .pipe(shareReplay({ bufferSize: 1, refCount: false }));

  /** The whole document: versions, languages, locales and game modes. */
  meta(): Observable<CatalogMeta> {
    return this.meta$;
  }

  /** The Data Dragon language a locale's pages read by default. */
  languageOf(locale: UiLocale): Observable<string> {
    return this.meta$.pipe(map((meta) => languageOf(meta, locale)));
  }

  /** Whole-string matcher of a game version. */
  versionMatcher(): Observable<RegExp> {
    return this.meta$.pipe(map(versionMatcher));
  }
}
