import { HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable, shareReplay } from 'rxjs';
import { BYPASS_TRANSFER_CACHE } from '../../http/interceptors/bypass-transfer-cache';
import type { CatalogMeta } from '../generated/models/catalog-meta';
import type { UiLocale } from '../generated/models/ui-locale';
import { MetaService } from '../generated/services/meta.service';
import { injectEmbeddedRevalidation } from './inject-embedded-revalidation';
import { languageOf } from './language-of';
import { versionMatcher } from './version-matcher';

/**
 * `/api/meta`, requested once per application and shared: the locale → language mapping and
 * the version pattern come from it, never from a copy written by hand. A failed request is
 * not cached: the next subscriber asks again. On the server, each render asks once, and the
 * process answers from its memo while it is fresh (`ApiResponseMemo`, `SsrHttpBackend`).
 *
 * In the browser, hydration reads the document the page embeds, then, once the app is
 * stable, the network's is read once and published if it differs: an old patch's page is
 * kept for months, and its embedded document would otherwise name a stale latest version
 * and miss the newer ones for the whole visit (selector, short URLs, "latest patch" link).
 * Readers that keep their subscription follow it; the others read the newest at once.
 */
@Injectable({ providedIn: 'root' })
export class ApiMeta {
  private readonly service = inject(MetaService);
  private readonly revalidate = injectEmbeddedRevalidation();
  private readonly meta$ = this.service.getMeta().pipe(
    this.revalidate(() =>
      this.service.getMeta(undefined, new HttpContext().set(BYPASS_TRANSFER_CACHE, true)),
    ),
    shareReplay({ bufferSize: 1, refCount: false }),
  );

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
