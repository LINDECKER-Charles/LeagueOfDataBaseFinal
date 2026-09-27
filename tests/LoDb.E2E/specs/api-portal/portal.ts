import { type APIRequestContext, expect, type Page } from '@playwright/test';
import { accountLinkIn, lastMailTo } from '../account/mailbox';

/** The portal of the signed-in account. */
export const PORTAL = '/en/account/api';

// `lodb_` and 40 hexadecimal digits, as the API issues them.
const SECRET = /^lodb_[0-9a-f]{40}$/;

/** The secret the page shows once, right after it issued it. */
export async function shownSecret(page: Page): Promise<string> {
  const field = page.getByTestId('api-key-secret');
  await expect(field).toHaveValue(SECRET);
  return field.inputValue();
}

/** The status `/v1/usage` answers to `secret`, as a key holder calls it, cookies aside. */
export async function usageStatus(request: APIRequestContext, secret: string): Promise<number> {
  const response = await request.get('/v1/usage', {
    headers: { Authorization: `Bearer ${secret}` },
  });
  return response.status();
}

/**
 * Follows the verification link of the last e-mail sent to `email` and waits for the page to
 * confirm it: the portal read before that still sees an unverified address.
 */
export async function verifyEmail(
  page: Page,
  request: APIRequestContext,
  email: string,
): Promise<void> {
  await page.goto(accountLinkIn(await lastMailTo(request, email), 'verify-email'));
  await expect(
    page.getByText('Your email address is confirmed. Have fun, summoner.'),
  ).toBeVisible();
}
