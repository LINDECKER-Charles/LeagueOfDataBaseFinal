import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID, REQUEST_CONTEXT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../api/api-base-url';
import { PLATFORM } from '../../platform/platform';
import type { PlatformService } from '../../platform/platform-service';
import type { RenderBudget } from '../render-budget';
import { clientHeaderInterceptor } from './client-header-interceptor';
import { isApiRequest } from './is-api-request';
import { serverApiTimeoutInterceptor } from './server-api-timeout-interceptor';

const ORIGIN = 'https://leagueofdatabase.com';

interface Options {
  readonly client?: string | null;
  readonly platform?: 'browser' | 'server';
  readonly renderBudget?: RenderBudget;
}

function setUp({
  client = null,
  platform = 'browser',
  renderBudget,
}: Options): HttpTestingController {
  const service: Pick<PlatformService, 'clientHeader'> = { clientHeader: () => client };
  const context = { apiOrigin: ORIGIN, selfOrigin: null, renderBudget };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([clientHeaderInterceptor, serverApiTimeoutInterceptor])),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: ORIGIN },
      { provide: PLATFORM, useValue: service },
      { provide: PLATFORM_ID, useValue: platform },
      { provide: REQUEST_CONTEXT, useValue: context },
    ],
  });
  return TestBed.inject(HttpTestingController);
}

function get(url: string): void {
  TestBed.inject(HttpClient).get(url).subscribe();
}

describe('isApiRequest', () => {
  it.each([
    [`${ORIGIN}/api/meta`, ORIGIN, true],
    [`${ORIGIN}/api/catalog/16.19.1/en_US/champions`, ORIGIN, true],
    [`${ORIGIN}/i18n/en.json`, ORIGIN, false],
    [`${ORIGIN}/apical`, ORIGIN, false],
    [`${ORIGIN}.evil.test/api/meta`, ORIGIN, false],
    ['https://ddragon.leagueoflegends.com/api/versions.json', ORIGIN, false],
    ['/api/meta', '', true],
    ['/i18n/en.json', '', false],
  ])('%s against %j: %s', (url, origin, expected) => {
    expect(isApiRequest(url, origin)).toBe(expected);
  });
});

describe('clientHeaderInterceptor', () => {
  it('names the app on API requests', () => {
    const http = setUp({ client: 'android/1.4.0' });

    get(`${ORIGIN}/api/meta`);

    expect(http.expectOne(`${ORIGIN}/api/meta`).request.headers.get('X-LoDb-Client')).toBe(
      'android/1.4.0',
    );
  });

  it('never sends it elsewhere', () => {
    const http = setUp({ client: 'android/1.4.0' });

    get('https://ddragon.leagueoflegends.com/cdn/16.19.1/data/en_US/item.json');

    const request = http.expectOne(() => true).request;
    expect(request.headers.has('X-LoDb-Client')).toBe(false);
  });

  it('sends nothing from the web', () => {
    const http = setUp({ client: null });

    get(`${ORIGIN}/api/meta`);

    expect(http.expectOne(`${ORIGIN}/api/meta`).request.headers.has('X-LoDb-Client')).toBe(false);
  });
});

describe('serverApiTimeoutInterceptor', () => {
  const NOW = Date.UTC(2026, 9, 10);

  beforeEach(() => vi.useFakeTimers({ now: NOW, toFake: ['Date'] }));
  afterEach(() => vi.useRealTimers());

  it('bounds the API calls of a server render without budget by default', () => {
    const http = setUp({ platform: 'server' });

    get(`${ORIGIN}/api/meta`);

    expect(http.expectOne(`${ORIGIN}/api/meta`).request.timeout).toBe(10_000);
  });

  it.each([
    ['a call its share of the render', NOW + 20_000, 10_000],
    ['a late call the time left', NOW + 4_000, 4_000],
    ['a call past the deadline next to nothing', NOW - 1_000, 1],
  ])('gives %s', (_case, deadline, timeout) => {
    const http = setUp({ platform: 'server', renderBudget: { deadline, callTimeoutMs: 10_000 } });

    get(`${ORIGIN}/api/meta`);

    expect(http.expectOne(`${ORIGIN}/api/meta`).request.timeout).toBe(timeout);
  });

  it('keeps a bound the caller chose', () => {
    const http = setUp({ platform: 'server' });

    TestBed.inject(HttpClient).get(`${ORIGIN}/api/meta`, { timeout: 1_000 }).subscribe();

    expect(http.expectOne(`${ORIGIN}/api/meta`).request.timeout).toBe(1_000);
  });

  it.each([
    ['in the browser', `${ORIGIN}/api/meta`, 'browser'],
    ['outside the API', `${ORIGIN}/i18n/en.json`, 'server'],
  ] as const)('leaves requests unbounded %s', (_case, url, platform) => {
    const http = setUp({ platform });

    get(url);

    expect(http.expectOne(url).request.timeout).toBeUndefined();
  });
});
