import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import type { AccountUser } from '../../../core/api/generated/models/account-user';
import type { LoginRequest } from '../../../core/api/generated/models/login-request';
import { AUTH_STRATEGY } from '../../../core/auth/strategy/auth-strategy-token';
import { accountUser } from '../../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../../core/auth/testing/fake-auth-strategy';
import { configureAdminTestBed } from '../testing/admin-test-bed';
import { openAdminPage } from '../testing/open-admin-page';
import { AdminLoginPage } from './admin-login-page';

@Component({ template: '' })
class Probe {}

const ADMIN = accountUser({ roles: ['Admin'], twoFactorEnabled: true, multiFactor: true });

function refusal(code: string): HttpErrorResponse {
  return new HttpErrorResponse({ status: HttpStatusCode.Unauthorized, error: { code } });
}

// The sign-in answers, in turn, each of `answers`: a refusal code or a signed-in account.
async function open(url: string, ...answers: (string | AccountUser)[]) {
  const strategy = new FakeAuthStrategy();
  const signIn = vi.spyOn(strategy, 'signIn');
  for (const answer of answers) {
    signIn.mockReturnValueOnce(
      typeof answer === 'string' ? throwError(() => refusal(answer)) : of({ user: answer }),
    );
  }
  configureAdminTestBed(
    [
      { path: 'admin/login', component: AdminLoginPage },
      { path: 'admin/enroll', component: Probe },
      { path: 'admin/**', component: Probe },
    ],
    [{ provide: AUTH_STRATEGY, useValue: strategy }],
  );
  const visit = await openAdminPage(url);
  return { ...visit, signIn, router: TestBed.inject(Router) };
}

function fill(page: HTMLElement, name: string, value: string): void {
  const field = page.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
  field.value = value;
}

async function submit(page: HTMLElement): Promise<void> {
  page.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
  await vi.waitFor(() => expect(page.querySelector('button[type="submit"]:disabled')).toBeNull());
}

async function signIn(
  page: HTMLElement,
  identifier = 'root',
  password = ' s3cret ',
): Promise<void> {
  fill(page, 'identifier', ` ${identifier} `);
  fill(page, 'password', password);
  await submit(page);
}

describe('AdminLoginPage', () => {
  it('asks for the second factor, then lands on the page asked for', async () => {
    const {
      page,
      harness,
      signIn: sent,
      router,
    } = await open('/admin/login?returnUrl=%2Fadmin%2Fusers', 'two-factor-required', ADMIN);

    await signIn(page);
    harness.detectChanges();
    expect(page.querySelector('input[name="twoFactorCode"]')).not.toBeNull();
    fill(page, 'twoFactorCode', '123 456');
    await submit(page);

    const expected: LoginRequest = {
      identifier: 'root',
      password: ' s3cret ',
      rememberMe: false,
      twoFactorCode: '123456',
    };
    expect(sent).toHaveBeenLastCalledWith(expected);
    await vi.waitFor(() => expect(router.url).toBe('/admin/users'));
  });

  it('puts the caret in the field to fill: the identifier, then the code', async () => {
    const { page, harness } = await open('/admin/login', 'two-factor-required');

    expect(document.activeElement).toBe(page.querySelector('input[name="identifier"]'));
    await signIn(page);
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(document.activeElement).toBe(page.querySelector('input[name="twoFactorCode"]'));
  });

  it('accepts a recovery code in place of the authenticator', async () => {
    const {
      page,
      harness,
      signIn: sent,
      router,
    } = await open('/admin/login', 'two-factor-required', ADMIN);

    await signIn(page);
    harness.detectChanges();
    page.querySelector<HTMLButtonElement>('button[type="button"]')!.click();
    harness.detectChanges();
    fill(page, 'recoveryCode', 'abcd-efgh');
    await submit(page);

    expect(sent.mock.lastCall?.[0]).toMatchObject({ recoveryCode: 'abcd-efgh' });
    await vi.waitFor(() => expect(router.url).toBe('/admin'));
  });

  it('sends an administrator without an authenticator to its enrolment', async () => {
    const { page, router } = await open('/admin/login', accountUser({ roles: ['Admin'] }));

    await signIn(page);

    await vi.waitFor(() => expect(router.url).toBe('/admin/enroll'));
  });

  it('tells an account that is no administrator the admin is not for it', async () => {
    const { page, harness, router } = await open('/admin/login', accountUser());

    await signIn(page);
    harness.detectChanges();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain(
      'admin.login.errors.refused',
    );
    expect(router.url).toBe('/admin/login');
  });

  it.each([
    ['invalid-credentials', 'admin.login.errors.invalid_credentials'],
    ['account-locked', 'admin.login.errors.locked'],
    ['server-exploded', 'admin.login.errors.generic'],
  ])('explains a refusal %s', async (code, key) => {
    const { page, harness } = await open('/admin/login', code);

    await signIn(page);
    harness.detectChanges();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain(key);
  });

  it('keeps the identifier, in focus, and empties the password after a refusal', async () => {
    const { page, harness } = await open('/admin/login', 'invalid-credentials');

    await signIn(page);
    harness.detectChanges();

    const identifier = page.querySelector<HTMLInputElement>('input[name="identifier"]');
    expect(identifier?.value).toBe(' root ');
    expect(document.activeElement).toBe(identifier);
    expect(page.querySelector<HTMLInputElement>('input[name="password"]')?.value).toBe('');
  });
});
