import type { HttpInterceptorFn } from '@angular/common/http';

/**
 * Catches the `426 Upgrade Required` the API answers to an app below the minimum version of
 * the client policy (L9.0), to show the blocking update screen. The web sends no
 * `X-LoDb-Client` header and never gets one. Registered by app.config.ts since L3.1; until
 * L9.0 fills it, every response passes unchanged.
 */
export const updateInterceptor: HttpInterceptorFn = (request, next) => next(request);
