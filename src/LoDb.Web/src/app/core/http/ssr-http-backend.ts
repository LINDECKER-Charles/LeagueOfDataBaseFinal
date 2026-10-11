import {
  FetchBackend,
  HttpErrorResponse,
  type HttpEvent,
  type HttpRequest,
  HttpResponse,
  HttpStatusCode,
} from '@angular/common/http';
import { Injectable } from '@angular/core';
import { type Observable, of, throwError } from 'rxjs';
import type { BuildFiles } from './build-files';
import { injectPageOrigin } from './inject-page-origin';
import { injectSsrRequestContext } from './inject-ssr-request-context';
import { sameOriginPath } from './same-origin-path';

// A JSON file of the build, answered from memory, or a 404 that never leaves the process.
function buildFileResponse(
  files: BuildFiles,
  path: string,
  request: HttpRequest<unknown>,
): Observable<HttpEvent<unknown>> {
  const body =
    request.method === 'GET' && request.responseType === 'json' ? files.get(path) : undefined;
  const url = request.urlWithParams;
  return body === undefined
    ? throwError(
        () =>
          new HttpErrorResponse({ status: HttpStatusCode.NotFound, statusText: 'Not Found', url }),
      )
    : of(new HttpResponse({ body, status: HttpStatusCode.Ok, statusText: 'OK', url }));
}

/**
 * Server-side HTTP backend of a render: what the process already holds is answered from
 * memory, the rest fetched. The page's own files (the i18n catalogues, the changelog) come
 * from the build files the server read at start-up, never over HTTP: no request to itself,
 * no log line, no parse per render. `/api/meta` comes from the process's memo while fresh.
 * A backend and not an interceptor: Angular makes relative URLs absolute in a root interceptor
 * that runs after every application interceptor, and the transfer cache keys a response
 * before that, on the relative URL the browser requests again; answered here, the response
 * is still embedded in the page as before. Outside `src/server.ts` (`ng serve`, the build's
 * prerender), there are no build files and the host server answers the page's files.
 */
@Injectable()
export class SsrHttpBackend extends FetchBackend {
  private readonly pageOrigin = injectPageOrigin();
  private readonly context = injectSsrRequestContext();

  override handle(request: HttpRequest<unknown>): Observable<HttpEvent<unknown>> {
    const files = this.context?.buildFiles ?? null;
    const path = this.pageOrigin === null ? null : sameOriginPath(request.url, this.pageOrigin);
    if (files !== null && path !== null) {
      return buildFileResponse(files, path, request);
    }
    const memo = this.context?.apiResponses;
    if (memo?.covers(request)) {
      return memo.answer(request, (fetched) => super.handle(fetched));
    }
    return super.handle(request);
  }
}
