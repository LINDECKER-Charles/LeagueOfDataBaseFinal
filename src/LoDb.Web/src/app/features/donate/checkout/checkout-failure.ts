import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';

// What the API's refusals mean to the donor; any other failure is the gateway's.
const FAILURES: Readonly<Partial<Record<number, string>>> = {
  [HttpStatusCode.BadRequest]: 'donate.error.invalid_amount',
  [HttpStatusCode.Forbidden]: 'donate.error.csrf',
  [HttpStatusCode.TooManyRequests]: 'donate.error.throttled',
  [HttpStatusCode.ServiceUnavailable]: 'donate.error.unavailable',
};

/**
 * Key of the message a refused checkout shows: an amount out of bounds, an expired session
 * (the forgery guard), too many attempts, the form closed meanwhile; Stripe unreachable,
 * the API unreachable or anything else reads as the gateway failing.
 */
export function checkoutFailure(error: unknown): string {
  const status = error instanceof HttpErrorResponse ? error.status : 0;
  return FAILURES[status] ?? 'donate.error.gateway';
}
