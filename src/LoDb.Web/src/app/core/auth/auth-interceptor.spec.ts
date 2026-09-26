import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../api/api-base-url';
import { authInterceptor } from './auth-interceptor';
import { AUTH_STRATEGY } from './strategy/auth-strategy-token';
import { FakeAuthStrategy } from './testing/fake-auth-strategy';

const ORIGIN = 'https://api.example';

interface Setup {
  readonly http: HttpTestingController;
  readonly strategy: FakeAuthStrategy;
}

function setUp(platform: 'browser' | 'server' = 'browser'): Setup {
  const strategy = new FakeAuthStrategy();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: ORIGIN },
      { provide: PLATFORM_ID, useValue: platform },
      { provide: AUTH_STRATEGY, useValue: strategy },
    ],
  });
  return { http: TestBed.inject(HttpTestingController), strategy };
}

function get(url: string): void {
  TestBed.inject(HttpClient).get(url).subscribe();
}

describe('authInterceptor', () => {
  it('hands the API requests to the strategy of the platform', () => {
    const { http, strategy } = setUp();

    get(`${ORIGIN}/api/account/builds`);

    const request = http.expectOne(`${ORIGIN}/api/account/builds`).request;
    expect(request.headers.get('Authorization')).toBe('Bearer fake');
    expect(strategy.authorized.map(({ url }) => url)).toEqual([`${ORIGIN}/api/account/builds`]);
  });

  it.each([
    'https://ddragon.leagueoflegends.com/cdn/16.19.1/data/en_US/item.json',
    `${ORIGIN}/i18n/fr.json`,
    `${ORIGIN}.evil.example/api/account/me`,
  ])('never lets a credential leave for %s', (url) => {
    const { http, strategy } = setUp();

    get(url);

    expect(http.expectOne(url).request.headers.has('Authorization')).toBe(false);
    expect(strategy.authorized).toEqual([]);
  });

  it('keeps a server render anonymous', () => {
    const { http, strategy } = setUp('server');

    get(`${ORIGIN}/api/catalog/16.19.1/en_US/champions`);

    const request = http.expectOne(`${ORIGIN}/api/catalog/16.19.1/en_US/champions`).request;
    expect(request.headers.has('Authorization')).toBe(false);
    expect(strategy.authorized).toEqual([]);
  });
});
