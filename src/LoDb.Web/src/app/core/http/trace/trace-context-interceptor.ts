import { isPlatformServer } from '@angular/common';
import type { HttpInterceptorFn } from '@angular/common/http';
import { PLATFORM_ID, REQUEST, inject } from '@angular/core';
import { API_BASE_URL } from '../../api/api-base-url';
import { isApiRequest } from '../interceptors/is-api-request';
import { traceIdFromTraceparent } from './trace-id-from-traceparent';

const TRACEPARENT = 'traceparent';

/**
 * Passes the trace of the page request on to the API calls of its server render: nginx starts
 * a W3C trace for every request it proxies (docker/nginx/snippets/proxy-headers.conf), so the
 * API's log lines carry the trace id of the renderer's `http.request.served`. The renderer
 * records no span of its own: it hands the header on unchanged, as W3C Trace Context asks of
 * a participant that does not trace. An invalid header is not passed on, and the browser
 * starts no trace.
 */
export const traceContextInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isPlatformServer(inject(PLATFORM_ID)) || !isApiRequest(request.url, inject(API_BASE_URL))) {
    return next(request);
  }
  const traceparent = inject(REQUEST, { optional: true })?.headers.get(TRACEPARENT)?.trim();
  return traceIdFromTraceparent(traceparent) === undefined
    ? next(request)
    : next(request.clone({ setHeaders: { [TRACEPARENT]: traceparent ?? '' } }));
};
