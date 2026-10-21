import { HttpContextToken } from '@angular/common/http';

/**
 * Marks a request the HTTP transfer cache must leave to the network, even while the browser
 * still hydrates. The generated client takes an `HttpContext`, not HttpClient's own
 * `transferCache` option: `bypassTransferCacheInterceptor` turns the one into the other.
 */
export const BYPASS_TRANSFER_CACHE = new HttpContextToken<boolean>(() => false);
