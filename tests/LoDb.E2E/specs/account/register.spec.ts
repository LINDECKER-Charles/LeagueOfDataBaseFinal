import { discardAccount, type TestAccount } from '../../support/session';
import { expect, test } from '../../support/test';
import { deleteAccount, newAccount, register, signIn, signOut } from './accounts';
import { accountLinkIn, accountLinkMailedTo, lastMailTo } from './mailbox';

// The account's whole life across the API, its outbox and a mailbox: the links the e-mails
// carry, their token in the fragment, and the session cookies of a real browser exist only on
// the stack. Each rule of the forms is Vitest's or the API's.
const BANNER =
  'Confirm your email address to create builds and vote: an unconfirmed account is deleted after 7 days.';
// Meets every rule of the checklist, like the first one, and differs from it.
const NEW_PASSWORD = 'Shurima-Sun-Disc-2047!';
// The token of an e-mail link sits in its fragment, then leaves the address bar.
const TOKEN_IN_FRAGMENT = /\?user=\d+#token=[\w-]+$/;

// The account of the journey, deleted even when a step fails: no run leaves one behind.
let created: TestAccount | undefined;

test.afterEach(async ({ playwright, baseURL }) => {
  if (created) {
    await discardAccount(playwright.request, baseURL, created);
    created = undefined;
  }
});

// One account for the whole journey: the API limits how many are created in a row.
test('registers, confirms the e-mail, resets the password and signs back in', async ({
  page,
  request,
}) => {
  const account = (created = newAccount('reg'));
  const renewed = { ...account, password: NEW_PASSWORD };

  await test.step('registers and lands on the profile, asked to confirm', async () => {
    await register(page, account);
    await expect(page.getByRole('region', { name: BANNER })).toBeVisible();
    await expect(page.getByText(account.username).first()).toBeVisible();
  });

  await test.step('confirms the address through the link of the e-mail', async () => {
    const link = accountLinkIn(await lastMailTo(request, account.email), 'verify-email');
    expect(link).toMatch(TOKEN_IN_FRAGMENT);
    await page.goto(link);
    await expect(
      page.getByText('Your email address is confirmed. Have fun, summoner.'),
    ).toBeVisible();
    await expect(page).toHaveURL(/\/en\/account\/verify-email$/);
    await expect(page.getByRole('region', { name: BANNER })).toHaveCount(0);
  });

  await test.step('signs out through the account menu', async () => {
    await page.goto('/en/account/profile');
    await signOut(page);
    await page.locator('lodb-account-menu summary').click();
    await expect(page.getByRole('link', { name: 'Sign in' })).toBeVisible();
  });

  await test.step('sets a new password through the link of a reset e-mail', async () => {
    await page.goto('/en/account/forgot-password');
    await page.getByLabel('Email', { exact: true }).fill(account.email);
    await page.getByRole('button', { name: 'Send the link' }).click();
    await expect(page.getByText('a reset email has just been sent')).toBeVisible();
    const link = await accountLinkMailedTo(request, account.email, 'reset-password');
    expect(link).toMatch(TOKEN_IN_FRAGMENT);
    await page.goto(link);
    await expect(page).toHaveURL(/\/en\/account\/reset-password$/);
    await page.getByLabel('Password', { exact: true }).fill(NEW_PASSWORD);
    await page.getByLabel('Confirm password').fill(NEW_PASSWORD);
    await page.getByRole('button', { name: 'Reset password' }).click();
    await expect(page).toHaveURL(/\/en\/account\/login$/);
    created = renewed;
  });

  await test.step('signs back in with the new password, back on the profile', async () => {
    await signIn(page, account.username, NEW_PASSWORD);
    await expect(page).toHaveURL(/\/en\/account\/profile$/);
    await expect(page.getByRole('region', { name: BANNER })).toHaveCount(0);
  });

  await test.step('deletes the account', async () => {
    await deleteAccount(page, renewed);
    await page.goto('/en/account/login');
    await signIn(page, account.email, NEW_PASSWORD);
    // The wording of the refusal is login-view.spec.ts's.
    await expect(page.getByRole('alert')).toBeVisible();
    await expect(page).toHaveURL(/\/en\/account\/login$/);
  });
});
