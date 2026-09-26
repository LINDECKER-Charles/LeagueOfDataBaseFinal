import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Translation, TranslocoLoader } from '@jsverse/transloco';
import type { Observable } from 'rxjs';

/**
 * Loads `public/i18n/<locale>.json` through HttpClient with a relative URL. HttpClient is
 * the point: the SSR render waits for the request, the transfer cache embeds the response
 * in the page, and the browser reuses it instead of downloading the catalogue again. The
 * relative URL resolves against the page on the web and against the bundled files in the
 * shell build.
 */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);

  getTranslation(locale: string): Observable<Translation> {
    return this.http.get<Translation>(`i18n/${locale}.json`);
  }
}
