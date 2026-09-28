import { HttpStatusCode } from '@angular/common/http';
import type { TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { MfaSetup } from '../../../core/api/generated/models/mfa-setup';
import { AUTH_STRATEGY } from '../../../core/auth/strategy/auth-strategy-token';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { accountUser } from '../../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../../core/auth/testing/fake-auth-strategy';
import { ADMIN_API } from '../testing/admin-api';
import type { AdminVisit } from '../testing/admin-visit';
import { configureAdminTestBed } from '../testing/admin-test-bed';
import { openAdminPage } from '../testing/open-admin-page';
import { AdminEnrollPage } from './admin-enroll-page';

const SETUP: MfaSetup = {
  authenticatorUri: 'otpauth://totp/LODB:root%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=LODB',
  sharedKey: 'JBSWY3DPEHPK3PXP',
};
const START_URL = `${ADMIN_API}/api/admin/mfa/enrollment`;
const CONFIRM_URL = `${ADMIN_API}/api/admin/mfa/confirm`;

async function open(): Promise<AdminVisit> {
  configureAdminTestBed(
    [{ path: 'admin/enroll', component: AdminEnrollPage }],
    [{ provide: AUTH_STRATEGY, useValue: new FakeAuthStrategy() }],
  );
  return openAdminPage('/admin/enroll', async (http) => {
    const start = await vi.waitFor(() => http.expectOne(START_URL));
    expect(start.request.method).toBe('POST');
    start.flush(SETUP);
  });
}

async function send(visit: AdminVisit, code: string): Promise<TestRequest> {
  const input = visit.page.querySelector<HTMLInputElement>('input[name="code"]')!;
  input.value = code;
  visit.page.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
  return vi.waitFor(() => visit.http.expectOne(CONFIRM_URL));
}

describe('AdminEnrollPage', () => {
  it('draws the key as a QR code and as text in groups of four', async () => {
    const { page } = await open();

    expect(page.querySelector('lodb-qr-image svg path')?.getAttribute('d')).toMatch(
      /^M4 4h1v1h-1z/,
    );
    expect(page.querySelector('[data-shared-key]')?.textContent?.trim()).toBe(
      'JBSW Y3DP EHPK 3PXP',
    );
  });

  it('turns the second factor on, opens the session and shows the recovery codes', async () => {
    const visit = await open();
    const confirm = await send(visit, '123 456');

    expect(confirm.request.body).toEqual({ code: '123 456' });
    const admin = accountUser({ roles: ['Admin'], multiFactor: true, twoFactorEnabled: true });
    confirm.flush({ recoveryCodes: ['aaaa-bbbb', 'cccc-dddd'], session: { user: admin } });
    await visit.harness.fixture.whenStable();

    expect(TestBed.inject(AuthSession).isAdmin()).toBe(true);
    expect(visit.page.textContent).toContain('aaaa-bbbb');
    expect(visit.page.querySelector('a[href="/admin"]')).not.toBeNull();
  });

  it.each([
    [
      { code: 'validation-failed', errors: { code: ['invalid-code'] } },
      HttpStatusCode.BadRequest,
      'admin.enroll.errors.invalid_code',
    ],
    [{ code: 'mfa-already-enrolled' }, HttpStatusCode.Conflict, 'admin.enroll.errors.already'],
    [{ code: 'account-locked' }, HttpStatusCode.TooManyRequests, 'admin.login.errors.locked'],
  ])('explains a refused code %j', async (problem, status, key) => {
    const visit = await open();
    const confirm = await send(visit, '000000');

    confirm.flush(problem, { status, statusText: 'x' });
    await visit.harness.fixture.whenStable();

    expect(visit.page.querySelector('[role="alert"]')?.textContent).toContain(key);
    expect(visit.page.querySelector('input[name="code"]')).not.toBeNull();
  });
});
