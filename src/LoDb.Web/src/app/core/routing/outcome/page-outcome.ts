import type { HttpStatusCode } from '@angular/common/http';

/**
 * What a navigation answers instead of its page: a redirect, a real 404 or a failure. On the
 * server it becomes the status and headers of the response (`PageResponse`); in the browser,
 * a navigation. Redirect locations are root-relative, query included.
 */
export type PageOutcome =
  | {
      readonly kind: 'redirect';
      readonly status: HttpStatusCode.MovedPermanently | HttpStatusCode.Found;
      readonly location: string;
    }
  | { readonly kind: 'not-found' }
  | {
      readonly kind: 'failure';
      readonly status: HttpStatusCode.InternalServerError | HttpStatusCode.ServiceUnavailable;
      /** `Retry-After` of the API's 503, passed on to the visitor and the proxy. */
      readonly retryAfter: string | null;
    };
