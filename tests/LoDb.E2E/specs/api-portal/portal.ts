import { type APIRequestContext, expect, type Locator, type Page } from '@playwright/test';

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

/** The toast saying `message`: the portal tells each outcome as the legacy flashes did. */
export function portalToast(page: Page, message: string): Locator {
  return page.locator('.hx-toaster').getByText(message, { exact: true });
}

/** The status `/v1/usage` answers to `secret`, as a key holder calls it, cookies aside. */
export async function usageStatus(request: APIRequestContext, secret: string): Promise<number> {
  const response = await request.get('/v1/usage', {
    headers: { Authorization: `Bearer ${secret}` },
  });
  return response.status();
}
