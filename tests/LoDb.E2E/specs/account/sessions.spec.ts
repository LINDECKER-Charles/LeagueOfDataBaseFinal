import type { BrowserContext, Page } from '@playwright/test';
import { expect, test } from '../../support/test';
import { type AdminAccount, createAdmin, enrollAdmin, signInAdmin } from '../admin/admin-account';

// The French copy of the profile (public/i18n/account/fr.json), which the admin specs use too.
const SIGN_OUT_EVERYWHERE = 'Se déconnecter de tous les appareils';
const SIGNED_OUT = 'Vous êtes déconnecté de tous vos appareils.';
const DELETED = 204;

async function signedInAs(page: Page): Promise<string | null> {
  const response = await page.request.get('/api/account/me');
  const session = (await response.json()) as {
    user: { username: string } | null;
  };
  return session.user?.username ?? null;
}

// Erases the account with its own session and its password, as its profile would.
async function discard(context: BrowserContext, page: Page, admin: AdminAccount): Promise<void> {
  const cookies = await context.cookies();
  const xsrf = cookies.find((cookie) => cookie.name === 'XSRF-TOKEN')?.value ?? '';
  const deleted = await page.request.post('/api/profile/delete', {
    data: { password: admin.password },
    headers: { 'X-XSRF-TOKEN': xsrf },
  });
  expect(deleted.status(), `the clean-up deletes ${admin.username}`).toBe(DELETED);
}

test.describe('sessions of an account', () => {
  test('sign out of every device, after a sign-in with a second factor', async ({
    page,
    context,
  }) => {
    // The CLI, the enrolment, then a second sign-in may wait for the next step of the code.
    test.slow();
    const admin = createAdmin('ses');
    await enrollAdmin(page, admin);
    expect(await signedInAs(page)).toBe(admin.username);

    await page.goto('/fr/account/profile');
    await page.getByRole('button', { name: SIGN_OUT_EVERYWHERE }).click();

    await expect(page).toHaveURL(/\/fr\/account\/login$/);
    await expect(page.getByText(SIGNED_OUT)).toBeVisible();
    expect(await signedInAs(page)).toBeNull();
    await page.goto('/admin');
    await expect(page).toHaveURL(/\/admin\/login\?returnUrl=%2Fadmin$/);

    // A new sign-in works, with a code the enrolment has not spent.
    await signInAdmin(page, admin);
    await expect(page).toHaveURL(/\/admin$/);
    expect(await signedInAs(page)).toBe(admin.username);
    await discard(context, page, admin);
  });
});
