import { HTTP_TRANSFER_CACHE_ORIGIN_MAP, HttpBackend } from '@angular/common/http';
import { type EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { API_BASE_URL } from '../api/api-base-url';
import { injectPageOrigin } from './inject-page-origin';
import { injectSsrRequestContext } from './inject-ssr-request-context';
import { SelfOriginFetchBackend } from './self-origin-fetch-backend';

/**
 * Responses fetched from the internal API origin are stored in the transfer cache under the
 * public origin, the one the browser requests: the browser then reuses them instead of
 * calling the API a second time. Server only, which Angular enforces.
 */
function transferCacheOriginMap(): Record<string, string> {
  const apiOrigin = injectSsrRequestContext()?.apiOrigin;
  const pageOrigin = injectPageOrigin();
  return apiOrigin && pageOrigin ? { [apiOrigin]: pageOrigin } : {};
}

/**
 * HTTP wiring of the SSR render (plan, section 5.2). Outside a request, during build-time
 * route extraction, nothing calls the API: an empty origin simply keeps URLs relative.
 */
export function provideSsrHttp(): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: API_BASE_URL, useFactory: () => injectSsrRequestContext()?.apiOrigin ?? '' },
    { provide: HTTP_TRANSFER_CACHE_ORIGIN_MAP, useFactory: transferCacheOriginMap },
    { provide: HttpBackend, useClass: SelfOriginFetchBackend },
  ]);
}
