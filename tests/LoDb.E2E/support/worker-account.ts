import type { Browser, BrowserContext, Page } from '@playwright/test';
import { discardAccount, type TestAccount } from '../specs/account/accounts';
import { createMember, signInMember } from './member-account';
import { expect, test as base } from './test';

export { expect } from './test';

/** The verified account of a worker, and the session its sign-in opened. */
interface WorkerAccount extends TestAccount {
  readonly session: Awaited<ReturnType<BrowserContext['storageState']>>;
}

interface MemberFixtures {
  /**
   * A page in a context of its own, signed in as the worker's account, on its profile. The
   * account is reset after the test: no build, no favorite, a private card.
   */
  readonly member: Page;
}

interface WorkerFixtures {
  readonly workerAccount: WorkerAccount;
}

const PROFILE = '/en/account/profile';
const NO_FAVORITES = { champion: null, item: null, rune: null, summoner: null, skin: null };
const NO_CONTENT = 204;
// The CLI starts a .NET host in the API container, then the sign-in: more than a test's
// default 30 seconds may pass on a loaded machine.
const SETUP_TIMEOUT_MS = 90_000;

// Signs the account in, in a context of its own, and returns the session it opened.
async function signedInSession(
  browser: Browser,
  baseURL: string | undefined,
  account: TestAccount,
) {
  const context = await browser.newContext({ baseURL });
  try {
    await signInMember(await context.newPage(), baseURL, account);
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
 * not try the registration itself. The API allows 5 registrations an hour from an address,
 * which a run of 4 workers, each restarted after a failure, would exceed: the account is
 * created by the CLI, not the form (createMember). It is deleted when the worker ends.
 */
export const test = base.extend<MemberFixtures, WorkerFixtures>({
  workerAccount: [
    async ({ browser, playwright }, use, workerInfo) => {
      const { baseURL } = workerInfo.project.use;
      const account = createMember('wrk', { verified: true });
      try {
        const session = await signedInSession(browser, baseURL, account);
        await use({ ...account, session });
      } finally {
        await discardAccount(playwright.request, baseURL, account);
      }
    },
    { scope: 'worker', timeout: SETUP_TIMEOUT_MS },
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
