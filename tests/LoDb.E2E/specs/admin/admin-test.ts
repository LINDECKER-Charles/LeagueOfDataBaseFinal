import type { APIRequest, BrowserContext, Page } from '@playwright/test';
import { createAdmin } from '../../support/cli-account';
import { eraseAccount } from '../../support/session';
import { expect, test as base } from '../../support/test';
import { type AdminAccount, enrollAdmin } from './admin-account';

export { expect } from '../../support/test';

type StorageState = Awaited<ReturnType<BrowserContext['storageState']>>;

interface AdminSession {
  readonly account: AdminAccount;
  /** The cookies of the session the enrolment opened, second factor included. */
  readonly state: StorageState;
}

interface AdminFixtures {
  /** The administrator of the worker, whose session every `page` of the spec opens with. */
  readonly admin: AdminAccount;
}

interface AdminWorkerFixtures {
  readonly adminSession: AdminSession;
}

// The CLI, then a sign-in and an enrolment, may wait for the next step of the authenticator.
const SESSION_TIMEOUT_MS = 180_000;

/**
 * The state of a context without any session. Within a test or a hook, every new browser or
 * request context takes the options of the test, whose `storageState` is the session of the
 * administrator: a context that must stay anonymous says so.
 */
export const NO_SESSION: StorageState = { cookies: [], origins: [] };

/** `api`, whose new contexts open without the session of the test. */
export function anonymous(api: APIRequest): APIRequest {
  return { newContext: (options) => api.newContext({ storageState: NO_SESSION, ...options }) };
}

/**
 * The `test` of the admin specs: each worker creates an administrator with `admin create`,
 * enrols its authenticator through the UI, and opens every `page` with that session. The
 * account is deleted once the worker is done.
 */
export const test = base.extend<AdminFixtures, AdminWorkerFixtures>({
  adminSession: [
    async ({ browser, playwright }, use, workerInfo) => {
      const baseURL = workerInfo.project.use.baseURL;
      const account: AdminAccount = createAdmin('adm');
      const context = await browser.newContext({ baseURL, storageState: NO_SESSION });
      await enrollAdmin(await context.newPage(), account);
      const state = await context.storageState();
      await context.close();
      await use({ account, state });
      await discardAdmin(playwright.request, baseURL, { account, state });
    },
    { scope: 'worker', timeout: SESSION_TIMEOUT_MS },
  ],
  storageState: async ({ adminSession }, use) => use(adminSession.state),
  admin: async ({ adminSession }, use) => use(adminSession.account),
});

// Deletes the administrator with its own session: a sign-in by password alone would stop at
// the second factor.
async function discardAdmin(
  api: APIRequest,
  baseURL: string | undefined,
  session: AdminSession,
): Promise<void> {
  const origin = new URL(baseURL ?? '').origin;
  const context = await api.newContext({
    baseURL,
    storageState: session.state,
    extraHTTPHeaders: { Origin: origin },
  });
  try {
    await eraseAccount(context, session.account);
  } finally {
    await context.dispose();
  }
}

/** Opens a panel of the admin and waits for its heading. */
export async function openPanel(page: Page, path: string, title: string): Promise<void> {
  await page.goto(path);
  await expect(page.getByRole('heading', { level: 1, name: title, exact: true })).toBeVisible();
}
