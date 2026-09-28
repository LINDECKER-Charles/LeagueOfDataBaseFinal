import { expect, test } from '../../support/test';
import {
  deleteAccount,
  discardAccount,
  newAccount,
  register,
  signIn,
  signOut,
  type TestAccount,
} from './accounts';
import { accountLinkIn, lastMailTo } from './mailbox';

const BANNER = 'Confirm your email address to unlock build creation.';

// The account of the journey, deleted even when a step fails: no run leaves one behind.
let created: TestAccount | undefined;

test.afterEach(async ({ playwright, baseURL }) => {
  if (created) {
    await discardAccount(playwright.request, baseURL, created);
    created = undefined;
  }
});

// One account for the whole journey: the API limits how many are created in a row.
test('registers, confirms the e-mail, signs out and back in', async ({ page, request }) => {
  const account = (created = newAccount('reg'));

  await test.step('registers and lands on the profile, asked to confirm', async () => {
    await register(page, account);
    await expect(page.getByRole('region', { name: BANNER })).toBeVisible();
    await expect(page.getByText(account.username).first()).toBeVisible();
  });

  await test.step('confirms the address through the link of the e-mail', async () => {
    const link = accountLinkIn(await lastMailTo(request, account.email), 'verify-email');
    await page.goto(link);
    await expect(
      page.getByText('Your email address is confirmed. Have fun, summoner.'),
    ).toBeVisible();
    await expect(page.getByRole('region', { name: BANNER })).toHaveCount(0);
  });

  await test.step('signs out through the account menu', async () => {
    await page.goto('/en/account/profile');
    await signOut(page);
    await page.locator('lodb-account-menu summary').click();
    await expect(page.getByRole('link', { name: 'Sign in' })).toBeVisible();
  });

  await test.step('signs back in with the summoner name, back on the profile', async () => {
    await page.goto('/en/account/login');
    await signIn(page, account.username, account.password);
    await expect(page).toHaveURL(/\/en\/account\/profile$/);
    await expect(page.getByRole('region', { name: BANNER })).toHaveCount(0);
  });

  await test.step('deletes the account', async () => {
    await deleteAccount(page, account);
    await page.goto('/en/account/login');
    await signIn(page, account.email, account.password);
    await expect(page.getByRole('alert')).toHaveText('Incorrect identifier or password.');
  });
});
