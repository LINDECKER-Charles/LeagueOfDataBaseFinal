import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../api/api-base-url';
import type { AuthStrategyKind } from '../platform/auth-strategy-kind';
import { PLATFORM } from '../platform/platform';
import type { PlatformService } from '../platform/platform-service';
import { CookieAuthStrategy } from './cookie/cookie-auth-strategy';
import { provideAuth } from './provide-auth';
import { AuthSession } from './session/auth-session';
import { AUTH_STRATEGY } from './strategy/auth-strategy-token';
import { AuthStrategies } from './strategy/auth-strategies';
import { FakeAuthStrategy } from './testing/fake-auth-strategy';

const ORIGIN = 'https://leagueofdatabase.com';

// What a platform of `core/platform` does for its own strategy: registers it while it is
// built, which the detection does before anything injects `AUTH_STRATEGY`.
@Injectable({ providedIn: 'root' })
class HostStrategy extends FakeAuthStrategy {}

@Injectable({ providedIn: 'root' })
class HostPlatform {
  readonly authStrategy: AuthStrategyKind = 'host';

  constructor() {
    inject(AuthStrategies).register('host', HostStrategy);
  }
}

function setUp(platform: () => Pick<PlatformService, 'authStrategy'>, id = 'browser'): void {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideAuth(),
      { provide: API_BASE_URL, useValue: ORIGIN },
      { provide: PLATFORM, useFactory: platform },
      { provide: PLATFORM_ID, useValue: id },
    ],
  });
}

describe('provideAuth', () => {
  it('gives the web its cookie strategy', () => {
    setUp(() => ({ authStrategy: 'cookie' }));

    expect(TestBed.inject(AUTH_STRATEGY)).toBe(TestBed.inject(CookieAuthStrategy));
  });

  it('takes the strategy a platform registered from its own folder', () => {
    setUp(() => inject(HostPlatform));

    expect(TestBed.inject(AUTH_STRATEGY)).toBe(TestBed.inject(HostStrategy));
  });

  it('fails loudly on a strategy no platform registered', () => {
    setUp(() => ({ authStrategy: 'bearer' }));

    expect(() => TestBed.inject(AUTH_STRATEGY)).toThrowError(
      "No AuthStrategy is registered for 'bearer'.",
    );
  });

  it('reads the session in the browser once the first render is done', async () => {
    setUp(() => ({ authStrategy: 'cookie' }));
    const http = TestBed.inject(HttpTestingController);

    http.expectNone(`${ORIGIN}/api/account/me`);
    TestBed.tick();
    http.expectOne(`${ORIGIN}/api/account/me`).flush({ user: null });

    await vi.waitFor(() => expect(TestBed.inject(AuthSession).status()).toBe('anonymous'));
  });

  it('never reads it during a server render', () => {
    setUp(() => ({ authStrategy: 'cookie' }), 'server');
    const http = TestBed.inject(HttpTestingController);

    TestBed.tick();

    http.expectNone(`${ORIGIN}/api/account/me`);
    expect(TestBed.inject(AuthSession).status()).toBe('unknown');
  });
});
