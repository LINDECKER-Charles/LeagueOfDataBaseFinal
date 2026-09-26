import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { AUTH_STRATEGY } from '../../../../core/auth/strategy/auth-strategy-token';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { accountUser } from '../../../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../../../core/auth/testing/fake-auth-strategy';
import { RegisterView } from './register-view';

@Component({ template: '' })
class Probe {}

const STRONG = 'Correct-horse-42';

// Without catalogues, every text renders as its key: the spec reads which message is shown.
async function open() {
  document.documentElement.lang = 'fr';
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: AUTH_STRATEGY, useValue: new FakeAuthStrategy() },
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([
        { path: ':locale/account/register', component: RegisterView },
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
  const harness = await RouterTestingHarness.create('/fr/account/register');
  await harness.fixture.whenStable();
  return { harness, host: harness.routeNativeElement as HTMLElement };
}

async function type(harness: RouterTestingHarness, id: string, value: string) {
  const field = (harness.routeNativeElement as HTMLElement).querySelector<HTMLInputElement>(
    `#${id}`,
  )!;
  field.value = value;
  field.dispatchEvent(new Event('input'));
  await harness.fixture.whenStable();
}

async function fill(harness: RouterTestingHarness, password: string, confirmation = password) {
  await type(harness, 'register-email', 'faker@example.com');
  await type(harness, 'register-username', 'Faker');
  await type(harness, 'register-password', password);
  await type(harness, 'register-confirmation', confirmation);
  const host = harness.routeNativeElement as HTMLElement;
  host.querySelector<HTMLInputElement>('input[name=acceptTerms]')!.checked = true;
}

async function submit(harness: RouterTestingHarness) {
  const form = (harness.routeNativeElement as HTMLElement).querySelector('form')!;
  form.dispatchEvent(new Event('submit', { cancelable: true }));
  await harness.fixture.whenStable();
}

// Resolves the pending request, then one turn of the event loop settles what follows it.
async function settle(harness: RouterTestingHarness) {
  await new Promise((resolve) => setTimeout(resolve));
  await harness.fixture.whenStable();
}

// A rule reads as its key, then whether it is met for screen readers: the key comes first.
function metRules(host: HTMLElement): string[] {
  return [...host.querySelectorAll('.pwd-checklist__item--ok')].map(
    (item) => item.textContent?.trim().split(/\s+/)[0] ?? '',
  );
}

describe('RegisterView', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  // The specs share one document: the locale of this page must not leak into the next ones.
  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
  });

  it('checks the password as it is typed, with the rules of the server', async () => {
    const { harness, host } = await open();

    await type(harness, 'register-password', 'short1');
    expect(host.querySelectorAll('.pwd-checklist__item')).toHaveLength(6);
    expect(metRules(host)).toEqual(['auth.password.rule_lowercase', 'auth.password.rule_digit']);

    await type(harness, 'register-password', STRONG);
    await type(harness, 'register-confirmation', STRONG);
    expect(host.querySelectorAll('.pwd-checklist__item--ok')).toHaveLength(6);
  });

  it('sends nothing while the confirmation differs', async () => {
    const { harness, host } = await open();

    await fill(harness, STRONG, `${STRONG}!`);
    await submit(harness);

    TestBed.inject(HttpTestingController).expectNone('/api/account/register');
    expect(host.textContent).toContain('auth.register.password_mismatch');
  });

  it('signs the new account in and lands on its profile', async () => {
    const { harness } = await open();
    await fill(harness, STRONG);
    await submit(harness);

    const request = TestBed.inject(HttpTestingController).expectOne('/api/account/register');
    expect(request.request.body).toEqual({
      email: 'faker@example.com',
      username: 'Faker',
      password: STRONG,
      acceptTerms: true,
      locale: 'fr',
    });
    request.flush({ user: accountUser() });
    await settle(harness);

    expect(TestBed.inject(AuthSession).isAuthenticated()).toBe(true);
    expect(TestBed.inject(Router).url).toBe('/fr/account/profile');
  });

  it('says under each field what the server refused', async () => {
    const { harness, host } = await open();
    await fill(harness, STRONG);
    await submit(harness);

    TestBed.inject(HttpTestingController)
      .expectOne('/api/account/register')
      .flush(
        { code: 'validation-failed', errors: { email: ['email-taken'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle(harness);

    expect(host.querySelector('#register-email')?.getAttribute('aria-invalid')).toBe('true');
    expect(host.textContent).toContain('account.errors.email_taken');
    expect(TestBed.inject(Router).url).toBe('/fr/account/register');
  });
});
