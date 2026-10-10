import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import type { PageOutcome } from '../outcome/page-outcome';

// How HttpClient reports a request that got no response: unreachable API, call timed out.
const NO_RESPONSE = 0;
// No answer from the API, or a proxy's in its place: the API is down or too slow.
const UNANSWERED: readonly number[] = [
  NO_RESPONSE,
  HttpStatusCode.BadGateway,
  HttpStatusCode.GatewayTimeout,
];

type Failure = Extract<PageOutcome, { kind: 'failure' }>;

/**
 * The failure a navigation error answers. A 503 only when the API itself shed the load
 * (a cold version, a busy gate), which the visitor may retry after the API's `Retry-After`:
 * a 503 always means a load shed on purpose. A 502 when the API did not answer, an outage
 * that must not pass for shedding; a 500 otherwise.
 */
export function failureOf(error: unknown): Failure {
  if (!(error instanceof HttpErrorResponse)) {
    return { kind: 'failure', status: HttpStatusCode.InternalServerError, retryAfter: null };
  }
  if (error.status === HttpStatusCode.ServiceUnavailable) {
    const retryAfter = error.headers.get('Retry-After');
    return { kind: 'failure', status: HttpStatusCode.ServiceUnavailable, retryAfter };
  }
  const status = UNANSWERED.includes(error.status)
    ? HttpStatusCode.BadGateway
    : HttpStatusCode.InternalServerError;
  return { kind: 'failure', status, retryAfter: null };
}
