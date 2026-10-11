import {
  HttpErrorResponse,
  type HttpEvent,
  type HttpRequest,
  HttpResponse,
  HttpStatusCode,
} from '@angular/common/http';
import { type Observable, catchError, of, tap, throwError } from 'rxjs';
import { deepFreeze } from './deep-freeze';

interface Memoised {
  readonly response: HttpResponse<unknown>;
  readonly fetchedAt: number;
}

// No response (unreachable, timed out) or a server error: the API could not answer now.
function isUnanswered(error: unknown): boolean {
  return (
    !(error instanceof HttpErrorResponse) ||
    error.status === 0 ||
    error.status >= HttpStatusCode.InternalServerError
  );
}

/**
 * The server process's memo of the API reads every render repeats with the same answer
 * (`/api/meta`, the same for every visitor: the renderer sends no cookie). A successful
 * answer serves every render for `ttlMs`; past it, the next render that needs it asks again,
 * and when the API cannot answer, it gets the last good copy rather than failing. Nothing
 * else is ever stored, a failure included. Created once by `src/server.ts`, which hands it to
 * each render through `SsrRequestContext`; the shared body is frozen.
 */
export class ApiResponseMemo {
  private readonly entries = new Map<string, Memoised>();

  constructor(
    private readonly urls: ReadonlySet<string>,
    private readonly ttlMs: number,
    private readonly now: () => number,
  ) {}

  /** Whether `request` is one this memo answers: a GET of one of its URLs, without query. */
  covers(request: HttpRequest<unknown>): boolean {
    return request.method === 'GET' && this.urls.has(request.urlWithParams);
  }

  /** Answers a covered request from the memo while fresh, otherwise through `fetch`. */
  answer(
    request: HttpRequest<unknown>,
    fetch: (request: HttpRequest<unknown>) => Observable<HttpEvent<unknown>>,
  ): Observable<HttpEvent<unknown>> {
    const memoised = this.entries.get(request.urlWithParams);
    if (memoised !== undefined && this.now() - memoised.fetchedAt < this.ttlMs) {
      return of(memoised.response);
    }
    return fetch(request).pipe(
      tap((event) => this.keep(request.urlWithParams, event)),
      catchError((error: unknown) =>
        memoised !== undefined && isUnanswered(error)
          ? of(memoised.response)
          : throwError(() => error),
      ),
    );
  }

  private keep(url: string, event: HttpEvent<unknown>): void {
    if (event instanceof HttpResponse && event.ok) {
      deepFreeze(event.body);
      this.entries.set(url, { response: event, fetchedAt: this.now() });
    }
  }
}
