import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import type { PageOutcome } from '../outcome/page-outcome';

// How HttpClient reports a request that got no response: unreachable API, aborted call.
const NO_RESPONSE = 0;
// No answer, or a proxy's: the API is down or busy, not the page broken.
const UNAVAILABLE: readonly number[] = [
  NO_RESPONSE,
  HttpStatusCode.BadGateway,
  HttpStatusCode.ServiceUnavailable,
  HttpStatusCode.GatewayTimeout,
];

/**
 * The failure a navigation error answers: a 503 when the API could not answer, which the
 * visitor may retry (after the API's `Retry-After` when it gave one), a 500 otherwise.
 */
export function failureOf(error: unknown): Extract<PageOutcome, { kind: 'failure' }> {
  if (error instanceof HttpErrorResponse && UNAVAILABLE.includes(error.status)) {
    const retryAfter = error.headers.get('Retry-After');
    return { kind: 'failure', status: HttpStatusCode.ServiceUnavailable, retryAfter };
  }
  return { kind: 'failure', status: HttpStatusCode.InternalServerError, retryAfter: null };
}
