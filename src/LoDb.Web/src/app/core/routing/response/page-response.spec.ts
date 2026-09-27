import { HttpStatusCode } from '@angular/common/http';
import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { PageOutcome } from '../outcome/page-outcome';
import { PageResponse } from './page-response';

const TRANSIENT = 'public, max-age=0, s-maxage=60';

function serverResponse(init: ResponseInit): PageResponse {
  TestBed.configureTestingModule({ providers: [{ provide: RESPONSE_INIT, useValue: init }] });
  return TestBed.inject(PageResponse);
}

function headersOf(init: ResponseInit): Record<string, string> {
  return Object.fromEntries(new Headers(init.headers).entries());
}

describe('PageResponse', () => {
  interface Case {
    readonly name: string;
    readonly outcome: PageOutcome;
    readonly status: number;
    readonly headers: Record<string, string>;
  }

  it.each<Case>([
    {
      name: 'a permanent redirect',
      outcome: {
        kind: 'redirect',
        status: HttpStatusCode.MovedPermanently,
        location: '/en/champions',
      },
      status: HttpStatusCode.MovedPermanently,
      headers: { location: '/en/champions' },
    },
    {
      name: 'a temporary redirect',
      outcome: {
        kind: 'redirect',
        status: HttpStatusCode.Found,
        location: '/en/15.14.1/items?page=2',
      },
      status: HttpStatusCode.Found,
      headers: { location: '/en/15.14.1/items?page=2' },
    },
    {
      name: 'a missing page',
      outcome: { kind: 'not-found' },
      status: HttpStatusCode.NotFound,
      headers: { 'x-robots-tag': 'noindex' },
    },
    {
      name: 'an API that asks to come back',
      outcome: { kind: 'failure', status: HttpStatusCode.ServiceUnavailable, retryAfter: '5' },
      status: HttpStatusCode.ServiceUnavailable,
      headers: { 'retry-after': '5', 'x-robots-tag': 'noindex' },
    },
    {
      name: 'a broken page',
      outcome: { kind: 'failure', status: HttpStatusCode.InternalServerError, retryAfter: null },
      status: HttpStatusCode.InternalServerError,
      headers: { 'x-robots-tag': 'noindex' },
    },
  ])('answers $name with its status, headers and a short cache', ({ outcome, status, headers }) => {
    const init: ResponseInit = { status: HttpStatusCode.Ok, headers: new Headers() };

    serverResponse(init).answer(outcome);

    expect(init.status).toBe(status);
    expect(headersOf(init)).toEqual({ ...headers, 'cache-control': TRANSIENT });
  });

  it('replaces the cache the server route configured', () => {
    const init: ResponseInit = { headers: new Headers({ 'Cache-Control': 'no-cache' }) };

    serverResponse(init).cache('archived');

    expect(headersOf(init)['cache-control']).toBe('public, max-age=3600, s-maxage=604800');
  });

  it('keeps a response it may not store out of caches, whatever is set afterwards', () => {
    const init: ResponseInit = { headers: new Headers() };
    const response = serverResponse(init);

    response.forbidStorage();
    response.cache('archived');
    response.answer({ kind: 'not-found' });

    expect(headersOf(init)['cache-control']).toBe('private, no-store');
  });

  it('keeps the headers given in another form', () => {
    const init: ResponseInit = { headers: { 'Content-Type': 'text/html' } };

    serverResponse(init).answer({ kind: 'not-found' });

    expect(init.headers).toBeInstanceOf(Headers);
    expect(headersOf(init)['content-type']).toBe('text/html');
  });

  it('writes nothing without a server response, in the browser or while prerendering', () => {
    const response = TestBed.inject(PageResponse);

    expect(() => {
      response.answer({ kind: 'not-found' });
      response.cache('latest');
    }).not.toThrow();
  });
});
