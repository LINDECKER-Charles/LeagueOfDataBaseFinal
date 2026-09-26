import { FetchBackend, type HttpEvent, type HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import { injectPageOrigin } from './inject-page-origin';
import { injectSsrRequestContext } from './inject-ssr-request-context';
import { rewriteToSelfOrigin } from './rewrite-to-self-origin';

/**
 * Server-side fetch backend that serves same-origin requests from the SSR server itself.
 * A backend and not an interceptor: Angular turns relative URLs into absolute ones in a root
 * interceptor that runs after every application interceptor, so only the backend sees the
 * final URL. The transfer cache keys the response before that, on the relative URL, which is
 * exactly what the browser requests again.
 */
@Injectable()
export class SelfOriginFetchBackend extends FetchBackend {
  private readonly pageOrigin = injectPageOrigin();
  private readonly selfOrigin = injectSsrRequestContext()?.selfOrigin ?? null;

  override handle(request: HttpRequest<unknown>): Observable<HttpEvent<unknown>> {
    if (this.pageOrigin === null || this.selfOrigin === null) {
      return super.handle(request);
    }
    const url = rewriteToSelfOrigin(request.url, this.pageOrigin, this.selfOrigin);
    return super.handle(url === request.url ? request : request.clone({ url }));
  }
}
