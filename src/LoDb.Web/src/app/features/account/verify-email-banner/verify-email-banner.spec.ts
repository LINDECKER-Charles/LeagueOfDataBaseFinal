import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID, type Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { AccountUser } from '../../../core/api/generated/models/account-user';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { AUTH_STRATEGY } from '../../../core/auth/strategy/auth-strategy-token';
import { accountUser } from '../../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../../core/auth/testing/fake-auth-strategy';
import { ToastService } from '../../../core/layout/toast/toast-service';
import { PLATFORM } from '../../../core/platform/platform';
import { VerifyEmailBanner } from './verify-email-banner';

const API = 'https://api.example.com';
// The chrome only reads the session in an application that detected its platform.
const DETECTED: Provider = { provide: PLATFORM, useValue: {} };

// Without catalogues, every text renders as its key: the spec reads which one is shown.
async function open(user: AccountUser | null, detected = true) {
  document.documentElement.lang = 'fr';
  const strategy = new FakeAuthStrategy();
  strategy.sessionAnswer = of({ user });
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: AUTH_STRATEGY, useValue: strategy },
      { provide: API_BASE_URL, useValue: API },
      detected ? DETECTED : [],
      provideHttpClient(),
      provideHttpClientTesting(),
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
  await TestBed.inject(AuthSession).load();
  const fixture = TestBed.createComponent(VerifyEmailBanner);
  await fixture.whenStable();
  return { fixture, host: fixture.nativeElement as HTMLElement };
}

describe('VerifyEmailBanner', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  // The specs share one document: the locale of this page must not leak into the next ones.
  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
  });

  it('asks an account whose e-mail is not verified to verify it', async () => {
    const { host } = await open(accountUser({ emailVerified: false }));

    expect(host.querySelector('[role=region]')?.getAttribute('aria-label')).toBe(
      'auth.verify.banner',
    );
  });

  it('shows nothing to a verified account, a visitor, or an undetected platform', async () => {
    expect((await open(accountUser())).host.children).toHaveLength(0);
    TestBed.resetTestingModule();
    expect((await open(null)).host.children).toHaveLength(0);
    TestBed.resetTestingModule();
    const unverified = accountUser({ emailVerified: false });
    expect((await open(unverified, false)).host.children).toHaveLength(0);
  });

  it('sends the link again in the locale of the page, and says it went', async () => {
    const { fixture, host } = await open(accountUser({ emailVerified: false }));

    host.querySelector('button')!.click();
    await fixture.whenStable();
    const request = TestBed.inject(HttpTestingController).expectOne(
      `${API}/api/account/verify-email/resend`,
    );
    expect(request.request.body).toEqual({ locale: 'fr' });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await vi.waitFor(() => expect(TestBed.inject(ToastService).toasts()).toHaveLength(1));

    expect(TestBed.inject(ToastService).toasts()[0]).toEqual(
      expect.objectContaining({ kind: 'success', message: 'auth.verify.resend_done' }),
    );
  });
});
