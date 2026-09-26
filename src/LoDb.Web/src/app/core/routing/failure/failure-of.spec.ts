import { HttpErrorResponse, HttpHeaders, HttpStatusCode } from '@angular/common/http';
import { failureOf } from './failure-of';

function httpError(status: number, retryAfter?: string): HttpErrorResponse {
  const headers =
    retryAfter === undefined ? undefined : new HttpHeaders({ 'Retry-After': retryAfter });
  return new HttpErrorResponse({ status, headers, url: 'http://api:8080/api/meta' });
}

describe('failureOf', () => {
  it.each([
    ['an unreachable or timed out API', httpError(0), null],
    ['a proxy without upstream', httpError(HttpStatusCode.BadGateway), null],
    ['a cold version being ingested', httpError(HttpStatusCode.ServiceUnavailable, '5'), '5'],
    ['a proxy timeout', httpError(HttpStatusCode.GatewayTimeout), null],
  ])('answers 503 for %s, passing its Retry-After on', (_case, error, retryAfter) => {
    expect(failureOf(error)).toEqual({
      kind: 'failure',
      status: HttpStatusCode.ServiceUnavailable,
      retryAfter,
    });
  });

  it.each([
    ['an API error', httpError(HttpStatusCode.InternalServerError)],
    ['an unexpected API answer', httpError(HttpStatusCode.Conflict)],
    ['a bug', new TypeError('undefined is not a function')],
    ['a failed lazy chunk', new Error('Failed to fetch dynamically imported module')],
    ['anything thrown', 'boom'],
  ])('answers 500 for %s', (_case, error) => {
    expect(failureOf(error)).toEqual({
      kind: 'failure',
      status: HttpStatusCode.InternalServerError,
      retryAfter: null,
    });
  });
});
