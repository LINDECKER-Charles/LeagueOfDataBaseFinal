import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID, REQUEST } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../api/api-base-url';
import { traceContextInterceptor } from './trace-context-interceptor';

const API_ORIGIN = 'http://lodb-api:8080';
const TRACEPARENT = '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00';

interface Options {
  readonly platform?: 'browser' | 'server';
  readonly traceparent?: string;
}

function setUp({ platform = 'server', traceparent }: Options): HttpTestingController {
  const headers: Record<string, string> = traceparent === undefined ? {} : { traceparent };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([traceContextInterceptor])),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: API_ORIGIN },
      { provide: PLATFORM_ID, useValue: platform },
      { provide: REQUEST, useValue: new Request('https://site.test/en/items', { headers }) },
    ],
  });
  return TestBed.inject(HttpTestingController);
}

function sentTraceparent(http: HttpTestingController, url: string): string | null {
  TestBed.inject(HttpClient).get(url).subscribe();
  return http.expectOne(url).request.headers.get('traceparent');
}

describe('traceContextInterceptor', () => {
  it("passes the page request's trace on to the API, unchanged", () => {
    const http = setUp({ traceparent: ` ${TRACEPARENT} ` });

    expect(sentTraceparent(http, `${API_ORIGIN}/api/meta`)).toBe(TRACEPARENT);
  });

  it.each([
    ['a request without trace', undefined],
    ['an invalid trace', '00-00000000000000000000000000000000-00f067aa0ba902b7-01'],
    ['a malformed header', 'not-a-trace'],
  ])('passes nothing on for %s', (_case, traceparent) => {
    const http = setUp({ traceparent });

    expect(sentTraceparent(http, `${API_ORIGIN}/api/meta`)).toBeNull();
  });

  it('never sends the trace outside the API', () => {
    const http = setUp({ traceparent: TRACEPARENT });

    expect(
      sentTraceparent(http, 'https://ddragon.leagueoflegends.com/api/versions.json'),
    ).toBeNull();
  });

  it('starts nothing in the browser', () => {
    const http = setUp({ platform: 'browser', traceparent: TRACEPARENT });

    expect(sentTraceparent(http, `${API_ORIGIN}/api/meta`)).toBeNull();
  });
});
