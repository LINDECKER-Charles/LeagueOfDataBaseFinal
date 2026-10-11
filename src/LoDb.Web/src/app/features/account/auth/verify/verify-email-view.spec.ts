import { Location } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../../../core/api/api-base-url';
import { AUTH_STRATEGY } from '../../../../core/auth/strategy/auth-strategy-token';
import { FakeAuthStrategy } from '../../../../core/auth/testing/fake-auth-strategy';
import { VerifyEmailView } from './verify-email-view';

@Component({ template: '' })
class Probe {}

const VERIFY_URL = '/api/account/verify-email';
const PAGE = '/fr/account/verify-email';
const RETURN = 'returnUrl=%2Ffr%2Fbuilds%2Fnew';

// Without catalogues, every text renders as its key: the spec reads which message is shown.
async function open(url: string) {
  document.documentElement.lang = 'fr';
  const strategy = new FakeAuthStrategy();
  strategy.sessionAnswer = of({ user: null });
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: AUTH_STRATEGY, useValue: strategy },
      { provide: API_BASE_URL, useValue: '' },
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([
        { path: ':locale/account/verify-email', component: VerifyEmailView },
        { path: '**', component: Probe },
      ]),
      provideTransloco({
        config: {
          availableLangs: ['fr'],
          defaultLang: 'fr',
          missingHandler: { logMissingKey: false },
        },
        loader: class {
          getTranslation = () => of({});
        },
      }),
    ],
  });
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  return { harness, host: harness.routeNativeElement as HTMLElement };
}

// Answers the verification the page sends when it opens, then lets what follows it settle.
async function answerVerification(harness: RouterTestingHarness, token: string) {
  const request = TestBed.inject(HttpTestingController).expectOne(VERIFY_URL);
  expect(request.request.body).toEqual({ userId: 7, token });
  request.flush({ alreadyVerified: false });
  await new Promise((resolve) => setTimeout(resolve));
  await harness.fixture.whenStable();
}

describe('VerifyEmailView', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  // The specs share one document: the locale of this page must not leak into the next ones.
  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
  });

  it('verifies the token of the fragment, then takes it out of the address', async () => {
    const { harness, host } = await open(`${PAGE}?user=7#token=t0k3n`);

    await answerVerification(harness, 't0k3n');

    expect(host.textContent).toContain('auth.flash.verify_success');
    expect(TestBed.inject(Router).url).toBe(PAGE);
    expect(TestBed.inject(Location).path(true)).toBe(PAGE);
  });

  it('still verifies a link sent before, its token in the query, keeping the rest', async () => {
    const { harness } = await open(`${PAGE}?user=7&token=t0k3n&${RETURN}`);

    await answerVerification(harness, 't0k3n');

    expect(TestBed.inject(Location).path(true)).toBe(`${PAGE}?${RETURN}`);
  });

  it('sends nothing and keeps the address without a token', async () => {
    const { harness } = await open(`${PAGE}?${RETURN}`);
    await harness.fixture.whenStable();

    TestBed.inject(HttpTestingController).expectNone(VERIFY_URL);
    expect(TestBed.inject(Location).path(true)).toBe(`${PAGE}?${RETURN}`);
  });
});
