import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Translation, TranslocoLoader } from '@jsverse/transloco';
import { type Observable, catchError, throwError } from 'rxjs';
import { PageResponse } from '../../routing/response/page-response';

/**
 * Loads `public/i18n/<locale>.json` through HttpClient with a relative URL. HttpClient is
 * the point: the SSR render waits for the request, the transfer cache embeds the response
 * in the page, and the browser reuses it instead of downloading the catalogue again. The
 * relative URL resolves against the page on the web and against the bundled files in the
 * shell build.
 *
 * A failed catalogue still lets the page render, its keys falling back to `en` or showing
 * raw, but the server response is then never stored: the proxy would serve it for minutes,
 * a week for a pinned version, where the legacy site always rendered its texts. Transloco
 * retries the load; a failed attempt is enough to keep the response out of caches.
 */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);
  private readonly response = inject(PageResponse);

  getTranslation(locale: string): Observable<Translation> {
    return this.http.get<Translation>(`i18n/${locale}.json`).pipe(
      catchError((error: unknown) => {
        this.response.forbidStorage();
        return throwError(() => error);
      }),
    );
  }
}
