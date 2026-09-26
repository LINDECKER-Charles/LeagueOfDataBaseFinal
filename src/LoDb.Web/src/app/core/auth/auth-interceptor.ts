import type { HttpInterceptorFn } from '@angular/common/http';

/**
 * Authenticates the requests through the `AuthStrategy` of the platform (plan, section 5.2):
 * session cookie on the web, tokens held by the desktop host, bearer tokens on Android.
 * Registered by app.config.ts since L3.1 so that L4.5 fills it without touching the
 * configuration; until then every request passes unchanged.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => next(request);
