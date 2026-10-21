import type { HttpInterceptorFn } from '@angular/common/http';
import { BYPASS_TRANSFER_CACHE } from './bypass-transfer-cache';

/**
 * Sets `transferCache: false` on a request marked `BYPASS_TRANSFER_CACHE`. Angular runs the
 * transfer cache as a root interceptor, after every interceptor the app registers, so it
 * reads the option this one sets.
 */
export const bypassTransferCacheInterceptor: HttpInterceptorFn = (request, next) =>
  next(
    request.context.get(BYPASS_TRANSFER_CACHE) ? request.clone({ transferCache: false }) : request,
  );
