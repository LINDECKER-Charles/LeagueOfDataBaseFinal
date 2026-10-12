import { type APIRequest, type APIRequestContext, expect, type Page } from '@playwright/test';

/** An account the suite creates, unique to its run. */
export interface TestAccount {
  readonly email: string;
  readonly username: string;
  readonly password: string;
}

// The sign-in issues it as a cookie; the API wants its value back on every unsafe request.
const XSRF_COOKIE = 'XSRF-TOKEN';
const UNAUTHORIZED = 401;
const NO_CONTENT = 204;

function xsrfTokenOf(cookies: readonly { readonly name: string; readonly value: string }[]) {
  return cookies.find((cookie) => cookie.name === XSRF_COOKIE)?.value ?? '';
}

/**
 * The headers of an unsafe request of the page's session: the API refuses one without the
 * site's origin, and a session's without its XSRF token.
 */
export async function unsafeHeaders(page: Page): Promise<Record<string, string>> {
  const origin = new URL(page.url()).origin;
  return { Origin: origin, 'X-XSRF-TOKEN': xsrfTokenOf(await page.context().cookies(origin)) };
}

/**
 * Erases the account `session` is signed in as, with its password, as its profile would.
 * `session` sends the site's origin with each request.
 */
export async function eraseAccount(
  session: APIRequestContext,
  account: Pick<TestAccount, 'username' | 'password'>,
): Promise<void> {
  const { cookies } = await session.storageState();
  const deleted = await session.post('/api/profile/delete', {
    data: { password: account.password },
    headers: { 'X-XSRF-TOKEN': xsrfTokenOf(cookies) },
  });
  expect(deleted.status(), `the clean-up deletes ${account.username}`).toBe(NO_CONTENT);
}

/**
 * Deletes the account through the API, whatever page a failed journey was left on: the
 * clean-up of a spec that creates an account. An account already deleted is left alone.
 */
export async function discardAccount(
  api: APIRequest,
  baseURL: string | undefined,
  account: TestAccount,
): Promise<void> {
  const origin = new URL(baseURL ?? '').origin;
  const session = await api.newContext({ baseURL, extraHTTPHeaders: { Origin: origin } });
  try {
    const login = await session.post('/api/account/login', {
      data: { identifier: account.email, password: account.password, rememberMe: false },
    });
    // Refused credentials: the journey got as far as deleting the account itself.
    if (login.status() === UNAUTHORIZED) {
      return;
    }
    expect(login.ok(), `the clean-up signs ${account.username} in`).toBe(true);
    await eraseAccount(session, account);
  } finally {
    await session.dispose();
  }
}
