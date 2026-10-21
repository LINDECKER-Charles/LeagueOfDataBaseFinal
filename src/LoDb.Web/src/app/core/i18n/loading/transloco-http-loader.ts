import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Translation, TranslocoLoader } from '@jsverse/transloco';
import { type Observable, catchError, throwError } from 'rxjs';
import { PageResponse } from '../../routing/response/page-response';

/**
 * Loads `i18n/<locale>.json` of the build through HttpClient with a relative URL. HttpClient
 * is the point: the server answers it from the catalogues it holds in memory
 * (`SsrHttpBackend`), the transfer cache embeds the response in the page, and the browser
 * reuses it instead of downloading the catalogue again. The relative URL resolves against the
 * page on the web and against the bundled files in the shell build. The build completes each
 * catalogue with the `en` messages it lacks (`npm run i18n:build`): a page loads its locale's
 * files alone, and Transloco shares the load of a path between its callers.
 *
 * A failed catalogue still lets the page render, Transloco falling back to the whole `en`
 * catalogue or the keys showing raw, but the server response is then never stored: the proxy
 * would serve it for minutes, a year for a pinned version, where the legacy site always
 * rendered its texts. Transloco retries the load; a failed attempt is enough to keep the
 * response out of caches. Nothing is kept here: a failure is requested again.
 */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);
  private readonly response = inject(PageResponse);

  getTranslation(path: string): Observable<Translation> {
    return this.http.get<Translation>(`i18n/${path}.json`).pipe(
      catchError((error: unknown) => {
        this.response.forbidStorage();
        return throwError(() => error);
      }),
    );
  }
}
