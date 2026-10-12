import type { Page } from '@playwright/test';
import { createMember, signInMember } from '../../support/member-account';
import { discardAccount } from '../../support/session';
import { expect, test } from '../../support/test';
import { signIn } from './accounts';

// Signing out of every device ends the sessions the API issued to a real browser as cookies:
// only the stack shows the next request arriving anonymous. The panel is Vitest's, the
// revocation the API's; a sign-in with a second factor is specs/admin/access.spec.ts's.
const PROFILE = '/en/account/profile';
const SIGNED_OUT = 'You are signed out of all your devices.';
// The CLI creates the member, then the sign-in: more than a test's default 30 seconds may pass.
const MEMBER_TIMEOUT_MS = 90_000;

async function signedInAs(page: Page): Promise<string | null> {
  const response = await page.request.get('/api/account/me');
  const session = (await response.json()) as { user: { username: string } | null };
  return session.user?.username ?? null;
}

test('signs out of every device, then back in', async ({ page, baseURL, playwright }) => {
  test.setTimeout(MEMBER_TIMEOUT_MS);
  // An account of its own: the worker's would lose the session its other journeys share.
  const account = createMember('ses', { verified: true });
  try {
    await signInMember(page, baseURL, account);
    expect(await signedInAs(page)).toBe(account.username);

    await page.getByRole('button', { name: 'Sign out of all devices' }).click();

    await expect(page).toHaveURL(/\/en\/account\/login$/);
    await expect(page.getByText(SIGNED_OUT)).toBeVisible();
    expect(await signedInAs(page)).toBeNull();

    await test.step('signs back in, on a session of its own', async () => {
      await page.goto(PROFILE);
      await expect(page).toHaveURL(/\/en\/account\/login\?returnUrl=/);
      await signIn(page, account.username, account.password);
      await expect(page).toHaveURL(new RegExp(`${PROFILE}$`));
      expect(await signedInAs(page)).toBe(account.username);
    });
  } finally {
    await discardAccount(playwright.request, baseURL, account);
  }
});
