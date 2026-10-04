import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from '../../api/api-base-url';
import { PLATFORM } from '../../platform/platform';
import { isApiRequest } from './is-api-request';

const CLIENT_HEADER = 'X-LoDb-Client';

/**
 * Names the app on every API request (`X-LoDb-Client: {platform}/{version}`, ADR 0008), so
 * the API can refuse a version under its minimum with a 426 (L9.0). The platform gives the
 * value; the web gives none and is never concerned.
 */
export const clientHeaderInterceptor: HttpInterceptorFn = (request, next) => {
  const client = inject(PLATFORM).clientHeader();
  if (client === null || !isApiRequest(request.url, inject(API_BASE_URL))) {
    return next(request);
  }
  return next(request.clone({ setHeaders: { [CLIENT_HEADER]: client } }));
};
