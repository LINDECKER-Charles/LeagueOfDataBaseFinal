import { expect, type Page } from '@playwright/test';

/** An account the suite creates, unique to its run. */
export interface TestAccount {
  readonly email: string;
  readonly username: string;
  readonly password: string;
}

// Meets every rule of the checklist, and is on no list of common passwords.
const PASSWORD = 'Hextech-Forge-2046!';
// The random part keeps two runs, or two workers, from sharing a name.
const RANDOM_LENGTH = 4;

/** A new account; `purpose` (a few letters) says which spec made it, in the database too. */
export function newAccount(purpose: string): TestAccount {
  const random = Math.random()
    .toString(36)
    .slice(2, 2 + RANDOM_LENGTH);
  // A summoner name is 3 to 24 letters, digits, dots, dashes or underscores.
  const username = `e2e_${purpose}_${Date.now().toString(36)}${random}`;
  return { email: `${username}@example.com`, username, password: PASSWORD };
}

/** Creates the account through the registration page, which lands on the profile. */
export async function register(page: Page, account: TestAccount): Promise<void> {
  await page.goto('/en/account/register');
  await page.getByLabel('Email', { exact: true }).fill(account.email);
  await page.getByLabel('Summoner name').fill(account.username);
  await page.getByLabel('Password', { exact: true }).fill(account.password);
  await page.getByLabel('Confirm password').fill(account.password);
  await page.getByLabel('I accept the terms of use').check();
  await page.getByRole('button', { name: 'Forge my account' }).click();
  await expect(page).toHaveURL(/\/en\/account\/profile$/);
}

/** Signs in through the login page, with an e-mail or a summoner name. */
export async function signIn(page: Page, identifier: string, password: string): Promise<void> {
  await page.getByLabel('Email or summoner name').fill(identifier);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Enter the archive' }).click();
}

/** Signs out through the account menu of the header. */
export async function signOut(page: Page): Promise<void> {
  await page.locator('lodb-account-menu summary').click();
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/en\/?$/);
}

/** Deletes the account from its profile, confirmed with its password, and lands home. */
export async function deleteAccount(page: Page, account: TestAccount): Promise<void> {
  await page.goto('/en/account/profile');
  await page.getByLabel('Current password').fill(account.password);
  await page.getByRole('button', { name: 'Delete my account' }).click();
  await expect(page).toHaveURL(/\/en\/?$/);
  await expect(page.getByText('Your account has been deleted. Farewell, summoner.')).toBeVisible();
}
