import { expect, type Page } from '@playwright/test';
import type { TestAccount } from '../specs/account/accounts';
import { createAdmin } from '../specs/admin/admin-account';
import { execInStack } from './stack';

interface MemberOptions {
  /** Whether the address counts as confirmed; the API creates builds for such accounts only. */
  readonly verified: boolean;
}

const PROFILE = '/en/account/profile';

// Sets the address state of the account and withdraws its administrator grant, in one
// statement. Prints how many grants it withdrew: 1, or the account was not found.
const DEMOTE = `
WITH member AS (
  UPDATE users SET is_verified = :'verified'::boolean WHERE email = :'email' RETURNING id
), withdrawn AS (
  DELETE FROM identity_user_roles WHERE user_id IN (SELECT id FROM member) RETURNING user_id
)
SELECT count(*) FROM withdrawn;
`;
// psql of the Postgres container, as its own superuser, quiet and unaligned.
const PSQL = 'exec psql -X -q -t -A -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" "$@"';

/**
 * A member account created without the registration form, whose quota (5 an hour from one
 * address) the suite keeps for account/register.spec.ts: `admin create` makes a verified
 * account, then the database withdraws its administrator grant, before any sign-in reads it.
 * `purpose` (a few letters) names the account. Its password is the one the CLI printed.
 */
export function createMember(purpose: string, { verified }: MemberOptions): TestAccount {
  const { email, username, password } = createAdmin(purpose);
  const variables = ['-v', `email=${email}`, '-v', `verified=${verified}`];
  const withdrawn = execInStack('postgres', ['sh', '-c', PSQL, 'psql', ...variables], DEMOTE);
  if (withdrawn.trim() !== '1') {
    throw new Error(`the database did not make ${email} a member: ${withdrawn}`);
  }
  return { email, username, password };
}

/**
 * Signs the account in through the API, in the page's context, then opens its profile: the
 * page is left where a registration leaves it, signed in.
 */
export async function signInMember(
  page: Page,
  baseURL: string | undefined,
  account: TestAccount,
): Promise<void> {
  // The API refuses an unsafe request without the site's origin.
  const origin = new URL(baseURL ?? '').origin;
  const login = await page.request.post('/api/account/login', {
    headers: { Origin: origin },
    data: { identifier: account.email, password: account.password, rememberMe: false },
  });
  expect(login.ok(), `${account.username} signs in`).toBe(true);
  await page.goto(PROFILE);
  await expect(page).toHaveURL(new RegExp(`${PROFILE}$`));
}
