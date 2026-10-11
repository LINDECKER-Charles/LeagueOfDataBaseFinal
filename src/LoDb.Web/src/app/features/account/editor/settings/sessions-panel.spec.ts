import { HttpErrorResponse } from '@angular/common/http';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { AUTH_STRATEGY } from '../../../../core/auth/strategy/auth-strategy-token';
import { accountUser } from '../../../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../../../core/auth/testing/fake-auth-strategy';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { SessionsPanel } from './sessions-panel';

// Without catalogues, every text renders as its key: the spec reads which one is shown.
async function open(strategy: FakeAuthStrategy) {
  document.documentElement.lang = 'fr';
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: AUTH_STRATEGY, useValue: strategy },
      provideRouter([]),
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
  const session = TestBed.inject(AuthSession);
  session.apply({ user: accountUser() });
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  const fixture = TestBed.createComponent(SessionsPanel);
  await fixture.whenStable();
  const host = fixture.nativeElement as HTMLElement;
  const click = async () => {
    host.querySelector('button')?.click();
    await vi.waitFor(() => expect(host.querySelector('button')?.disabled).toBe(false));
    await fixture.whenStable();
  };
  return { session, navigate, host, click };
}

describe('SessionsPanel', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
  });

  it('signs out of every session, then lands on the sign-in page', async () => {
    const strategy = new FakeAuthStrategy();
    const { session, navigate, click } = await open(strategy);

    await click();

    await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/fr/account/login'));
    expect(session.isAuthenticated()).toBe(false);
    expect(TestBed.inject(ToastService).toasts()).toEqual([
      expect.objectContaining({ kind: 'success', message: 'account.sessions.signed_out' }),
    ]);
  });

  it('stays signed in and says so when the API refuses', async () => {
    const strategy = new FakeAuthStrategy();
    strategy.signOutEverywhereAnswer = throwError(
      () => new HttpErrorResponse({ status: 503, error: { code: 'server-busy' } }),
    );
    const { session, navigate, host, click } = await open(strategy);

    await click();

    expect(session.isAuthenticated()).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
    expect(host.querySelector('[role=alert]')?.textContent?.trim()).toBe('account.errors.generic');
  });
});
