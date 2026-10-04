import { Component, PLATFORM_ID, type Provider } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import type { AccountUser } from '../../../core/api/generated/models/account-user';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { AUTH_STRATEGY } from '../../../core/auth/strategy/auth-strategy-token';
import { accountUser } from '../../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../../core/auth/testing/fake-auth-strategy';
import { ToastService } from '../../../core/layout/toast/toast-service';
import { PLATFORM } from '../../../core/platform/platform';
import { AccountMenu } from './account-menu';

@Component({ imports: [AccountMenu], template: '<lodb-account-menu />' })
class Header {}

// The chrome only reads the session in an application that detected its platform.
const DETECTED: Provider = { provide: PLATFORM, useValue: {} };

// Without catalogues, every text renders as its key: the spec reads which one is shown.
async function open(user: AccountUser | null, strategy = new FakeAuthStrategy(), detected = true) {
  document.documentElement.lang = 'fr';
  strategy.sessionAnswer = of({ user });
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: AUTH_STRATEGY, useValue: strategy },
      detected ? DETECTED : [],
      provideRouter([{ path: '**', component: Header }]),
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
  const harness = await RouterTestingHarness.create('/fr/champions');
  await harness.fixture.whenStable();
  return { harness, host: harness.routeNativeElement as HTMLElement };
}

function linksOf(host: HTMLElement): string[] {
  return [...host.querySelectorAll('a')].map((link) => link.getAttribute('href') ?? '');
}

async function signOut(harness: RouterTestingHarness) {
  const host = harness.routeNativeElement as HTMLElement;
  host.querySelector<HTMLButtonElement>('button')!.click();
  // The fake answers at once: one turn of the event loop settles the sign-out.
  await new Promise((resolve) => setTimeout(resolve));
  await harness.fixture.whenStable();
}

describe('AccountMenu', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  // The specs share one document: the locale of this page must not leak into the next ones.
  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
  });

  it('offers a visitor the sign-in and the registration, in the locale of the page', async () => {
    const { host } = await open(null);

    expect(linksOf(host)).toEqual(['/fr/account/login', '/fr/account/register']);
    expect(host.querySelector('summary')?.getAttribute('aria-label')).toBe('nav.account');
    expect(host.querySelector('summary span')?.textContent?.trim()).toBe('nav.account');
  });

  it('shows the same to every visitor of an application that never detected its platform', async () => {
    const { host } = await open(accountUser(), new FakeAuthStrategy(), false);

    expect(linksOf(host)).toEqual(['/fr/account/login', '/fr/account/register']);
  });

  it('names the account, cut short, and leads to its profile and builds', async () => {
    const { host } = await open(accountUser({ username: 'TheUnkillableDemonKing' }));

    expect(host.querySelector('summary span')?.textContent?.trim()).toBe('TheUnkillable…');
    expect(host.querySelector('summary')?.getAttribute('aria-label')).toBe(
      'TheUnkillableDemonKing',
    );
    expect(linksOf(host)).toEqual(['/fr/account/profile', '/fr/account/builds']);
  });

  it('signs out, then goes home as a visitor', async () => {
    const { harness, host } = await open(accountUser());

    await signOut(harness);

    expect(TestBed.inject(Router).url).toBe('/fr');
    expect(linksOf(host)).toEqual(['/fr/account/login', '/fr/account/register']);
  });

  it('keeps the session and says so when the sign-out fails', async () => {
    const strategy = new FakeAuthStrategy();
    strategy.signOutAnswer = throwError(() => new Error('offline'));
    const { harness } = await open(accountUser(), strategy);

    await signOut(harness);

    expect(TestBed.inject(ToastService).toasts()).toEqual([
      expect.objectContaining({ kind: 'error', message: 'account.errors.sign_out' }),
    ]);
    expect(TestBed.inject(Router).url).toBe('/fr/champions');
  });
});
