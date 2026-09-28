import { isPlatformServer } from '@angular/common';
import type { HttpInterceptorFn } from '@angular/common/http';
import { PLATFORM_ID, inject } from '@angular/core';
import { API_BASE_URL } from '../api/api-base-url';
import { isApiRequest } from '../http/interceptors/is-api-request';
import { AUTH_STRATEGY } from './strategy/auth-strategy-token';

/**
 * Authenticates the requests through the `AuthStrategy` of the platform (plan, section 5.2):
 * session cookie on the web, tokens held by the desktop host, bearer tokens on Android.
 * Only API requests reach the strategy: a token never leaves for a catalogue, Data Dragon
 * or a third party. A server render stays anonymous: its requests pass unchanged.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (isPlatformServer(inject(PLATFORM_ID)) || !isApiRequest(request.url, inject(API_BASE_URL))) {
    return next(request);
  }
  return inject(AUTH_STRATEGY).authorize(request, next);
};
