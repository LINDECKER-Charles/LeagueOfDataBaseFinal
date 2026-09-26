import { HttpErrorResponse } from '@angular/common/http';
import { Component, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { AUTH_STRATEGY } from '../../../../core/auth/strategy/auth-strategy-token';
import { FakeAuthStrategy } from '../../../../core/auth/testing/fake-auth-strategy';
import { LoginView } from './login-view';

@Component({ template: '' })
class Probe {}

// Without catalogues, every text renders as its key: the spec reads which message is shown.
async function open(url: string, strategy = new FakeAuthStrategy()) {
  document.documentElement.lang = 'fr';
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: AUTH_STRATEGY, useValue: strategy },
      provideRouter([
        { path: ':locale/account/login', component: LoginView },
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
  return { harness, host: harness.routeNativeElement as HTMLElement, strategy };
}

function refusal(code: string): HttpErrorResponse {
  return new HttpErrorResponse({ status: 400, error: { code } });
}

async function submit(harness: RouterTestingHarness, fields: Record<string, string>) {
  const host = harness.routeNativeElement as HTMLElement;
  for (const [id, value] of Object.entries(fields)) {
    host.querySelector<HTMLInputElement>(`#${id}`)!.value = value;
  }
  host.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
  // The fake answers at once: one turn of the event loop settles the sign-in.
  await new Promise((resolve) => setTimeout(resolve));
  await harness.fixture.whenStable();
}

const CREDENTIALS = { 'login-identifier': 'Faker', 'login-password': 'correct horse' };

describe('LoginView', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  // The specs share one document: the locale of this page must not leak into the next ones.
  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
  });

  it('signs in and lands on the profile, in the locale of the page', async () => {
    const { harness } = await open('/fr/account/login');

    await submit(harness, CREDENTIALS);

    expect(TestBed.inject(Router).url).toBe('/fr/account/profile');
  });

  it('goes back to the page it was sent from', async () => {
    const back = encodeURIComponent('/fr/champions');
    const { harness } = await open(`/fr/account/login?returnUrl=${back}`);

    await submit(harness, CREDENTIALS);

    expect(TestBed.inject(Router).url).toBe('/fr/champions');
  });

  it('says why the credentials were refused, and stays on the form', async () => {
    const strategy = new FakeAuthStrategy();
    strategy.signInAnswer = throwError(() => refusal('invalid-credentials'));
    const { harness, host } = await open('/fr/account/login', strategy);

    await submit(harness, CREDENTIALS);

    expect(host.querySelector('[role=alert]')?.textContent?.trim()).toBe(
      'account.errors.invalid_credentials',
    );
    expect(TestBed.inject(Router).url).toBe('/fr/account/login');
  });

  it('asks the authenticator code, then sends it with the password, spaces removed', async () => {
    const strategy = new FakeAuthStrategy();
    const signIn = vi.spyOn(strategy, 'signIn');
    signIn.mockReturnValueOnce(throwError(() => refusal('two-factor-required')));
    const { harness, host } = await open('/fr/account/login', strategy);

    await submit(harness, CREDENTIALS);
    expect(host.querySelector('#login-two-factor-code')).not.toBeNull();
    await submit(harness, { 'login-two-factor-code': '123 456' });

    expect(signIn).toHaveBeenLastCalledWith({
      identifier: 'Faker',
      password: 'correct horse',
      rememberMe: false,
      twoFactorCode: '123456',
    });
    expect(TestBed.inject(Router).url).toBe('/fr/account/profile');
  });

  it('shows why the Google sign-in came back without a session', async () => {
    const { host } = await open('/fr/account/login?error=google-cancelled');

    expect(host.querySelector('[role=alert]')?.textContent?.trim()).toBe(
      'account.errors.google_cancelled',
    );
  });
});
