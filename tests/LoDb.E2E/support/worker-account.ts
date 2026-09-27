import type { Browser, BrowserContext, Page } from '@playwright/test';
import { discardAccount, newAccount, register, type TestAccount } from '../specs/account/accounts';
import { accountLinkIn, lastMailTo } from '../specs/account/mailbox';
import { expect, test as base } from './test';

export { expect } from './test';

/** The verified account of a worker, and the session its registration opened. */
interface WorkerAccount extends TestAccount {
  readonly session: Awaited<ReturnType<BrowserContext['storageState']>>;
}

interface MemberFixtures {
  /**
   * A page in a context of its own, signed in as the worker's account, on its profile as a
   * registration leaves it. The account is reset after the test: no build, no favorite, a
   * private card.
   */
  readonly member: Page;
}

interface WorkerFixtures {
  readonly workerAccount: WorkerAccount;
}

const CONFIRMED = 'Your email address is confirmed.';
const PROFILE = '/en/account/profile';
const NO_FAVORITES = { champion: null, item: null, rune: null, summoner: null, skin: null };
const NO_CONTENT = 204;
// The registration waits for the verification e-mail, which the outbox sends in the
// background: more than a test's default 30 seconds may pass.
const REGISTRATION_TIMEOUT_MS = 90_000;

// Registers the account and confirms its address through the e-mail: the API creates builds
// for a verified account only. Returns the session the registration opened.
async function registerVerified(
  browser: Browser,
  baseURL: string | undefined,
  account: TestAccount,
) {
  const context = await browser.newContext({ baseURL });
  try {
    const page = await context.newPage();
    await register(page, account);
    await page.goto(accountLinkIn(await lastMailTo(page.request, account.email), 'verify-email'));
    await expect(page.getByText(CONFIRMED)).toBeVisible();
    return await context.storageState();
  } finally {
    await context.close();
  }
}

// The headers of an unsafe request of the page's session: the site's origin and the XSRF
// token the sign-in issued as a cookie.
async function unsafeHeaders(page: Page, baseURL: string | undefined) {
  const origin = new URL(baseURL ?? '').origin;
  const cookies = await page.context().cookies(origin);
  const xsrf = cookies.find((cookie) => cookie.name === 'XSRF-TOKEN')?.value ?? '';
  return { Origin: origin, 'X-XSRF-TOKEN': xsrf };
}

// Brings the account back to how the worker created it, whatever the test left behind.
async function resetAccount(page: Page, baseURL: string | undefined): Promise<void> {
  const headers = await unsafeHeaders(page, baseURL);
  const builds = await page.request.get('/api/builds');
  expect(builds.ok(), 'the reset lists the builds').toBe(true);
  for (const { id } of (await builds.json()) as { id: number }[]) {
    const deleted = await page.request.delete(`/api/builds/${id}`, { headers });
    expect(deleted.status(), `the reset deletes build ${id}`).toBe(NO_CONTENT);
  }
  const favorites = await page.request.put('/api/profile/favorites', {
    headers,
    data: NO_FAVORITES,
  });
  expect(favorites.ok(), 'the reset clears the favorites').toBe(true);
  const visibility = await page.request.put('/api/profile/visibility', {
    headers,
    data: { isPublic: false },
  });
  expect(visibility.ok(), 'the reset hides the card').toBe(true);
}

/**
 * The suite's `test`, plus one verified account per worker, shared by the journeys that do
 * not try the registration itself. The API allows 5 registrations an hour from an address:
 * one per worker keeps a run within it. The account is deleted when the worker ends.
 */
export const test = base.extend<MemberFixtures, WorkerFixtures>({
  workerAccount: [
    async ({ browser, playwright }, use, workerInfo) => {
      const { baseURL } = workerInfo.project.use;
      const account = newAccount('wrk');
      try {
        const session = await registerVerified(browser, baseURL, account);
        await use({ ...account, session });
      } finally {
        await discardAccount(playwright.request, baseURL, account);
      }
    },
    { scope: 'worker', timeout: REGISTRATION_TIMEOUT_MS },
  ],
  member: async ({ browser, baseURL, workerAccount }, use) => {
    const context = await browser.newContext({ baseURL, storageState: workerAccount.session });
    const page = await context.newPage();
    try {
      await page.goto(PROFILE);
      await expect(page).toHaveURL(new RegExp(`${PROFILE}$`));
      await use(page);
    } finally {
      await resetAccount(page, baseURL);
      await context.close();
    }
  },
});
