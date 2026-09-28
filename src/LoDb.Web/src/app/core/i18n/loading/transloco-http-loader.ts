import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Translation, TranslocoLoader } from '@jsverse/transloco';
import { type Observable, catchError, finalize, share, throwError } from 'rxjs';
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
 *
 * Concurrent loads of one path share a request: Transloco asks the main and the fallback
 * loader for a scope at once (`items/en` under the `en` fallback), and both name the same
 * file. Nothing is kept once it settles: Transloco caches what loaded, and a failure is
 * requested again.
 */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);
  private readonly response = inject(PageResponse);
  private readonly loads = new Map<string, Observable<Translation>>();

  getTranslation(path: string): Observable<Translation> {
    const pending = this.loads.get(path);
    if (pending !== undefined) {
      return pending;
    }
    const load: Observable<Translation> = this.http.get<Translation>(`i18n/${path}.json`).pipe(
      catchError((error: unknown) => {
        this.response.forbidStorage();
        return throwError(() => error);
      }),
      finalize(() => this.settled(path, load)),
      share(),
    );
    this.loads.set(path, load);
    return load;
  }

  // A later load of the path may already have taken the place: only the settled one leaves.
  private settled(path: string, load: Observable<Translation>): void {
    if (this.loads.get(path) === load) {
      this.loads.delete(path);
    }
  }
}
