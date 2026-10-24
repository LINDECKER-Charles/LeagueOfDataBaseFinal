import type { Browser, Page } from '@playwright/test';
import { createMember, signInMember } from '../../support/member-account';
import { discardAccount } from '../../support/session';
import { signInAdmin } from './admin-account';
import { anonymous, expect, NO_SESSION, openPanel, test } from './admin-test';

// Who reaches the admin: the guards, the authenticator's code and the panels' reports, against
// the real API and its cookies, in a browser. Only the stack enrols a real authenticator and
// opens a session with a second factor; the navigation and each panel are Vitest's.

// The CLI creates the member, then the sign-in: more than a test's default 30 seconds may pass.
const MEMBER_TIMEOUT_MS = 90_000;

// A browser without the administrator's session.
async function visitor(browser: Browser, baseURL: string | undefined): Promise<Page> {
  const context = await browser.newContext({ baseURL, storageState: NO_SESSION });
  return context.newPage();
}

test('sends a visitor to the login of the admin, kept out of the index', async ({
  browser,
  baseURL,
}) => {
  const page = await visitor(browser, baseURL);

  const response = await page.goto('/admin/users');

  expect(response?.headers()['x-robots-tag']).toContain('noindex');
  await expect(page).toHaveURL(/\/admin\/login\?returnUrl=%2Fadmin%2Fusers$/);
  await expect(
    page.getByRole('heading', { level: 1, name: "Panneau d'administration" }),
  ).toBeVisible();
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
});

test('hides the admin from a member, behind the 404 of the URL', async ({
  browser,
  baseURL,
  playwright,
  request,
}) => {
  test.setTimeout(MEMBER_TIMEOUT_MS);
  // Created by the CLI: the registration form's quota is account/register.spec.ts's.
  const account = createMember('admmbr', { verified: true });
  const page = await visitor(browser, baseURL);
  try {
    const copy = (await (await request.get('/i18n/seo/en.json')).json()) as {
      error: { '404': { title: string } };
    };
    await signInMember(page, baseURL, account);

    await page.goto('/admin/users');

    await expect(page).toHaveURL(/\/admin\/users$/);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('404');
    await expect(page.getByRole('main')).toContainText(copy.error['404'].title);
    await expect(
      page.getByRole('navigation', { name: "Navigation de l'administration" }),
    ).toHaveCount(0);
  } finally {
    await page.context().close();
    await discardAccount(anonymous(playwright.request), baseURL, account);
  }
});

test('signs the administrator in with the code of the authenticator, then out', async ({
  browser,
  baseURL,
  admin,
}) => {
  // The code of the enrolment may still be the current one: the sign-in waits for the next.
  test.slow();
  const page = await visitor(browser, baseURL);
  await page.goto('/admin/journal');
  await expect(page).toHaveURL(/\/admin\/login\?returnUrl=%2Fadmin%2Fjournal$/);

  await signInAdmin(page, admin);

  // The return URL of the guard is honoured once the second factor is in.
  await expect(page).toHaveURL(/\/admin\/journal$/);
  await expect(page.getByRole('heading', { level: 1, name: "Journal d'audit" })).toBeVisible();
  await page.getByRole('button', { name: 'Déconnexion' }).click();
  await expect(page).toHaveURL(/\/admin\/login$/);
  await page.goto('/admin');
  await expect(page).toHaveURL(/\/admin\/login\?returnUrl=%2Fadmin$/);
});

test('opens on the dashboard, then moves to another panel', async ({ page, consoleErrors }) => {
  await openPanel(page, '/admin', "Vue d'ensemble");

  const nav = page.getByRole('navigation', { name: "Navigation de l'administration" });
  await expect(nav.getByRole('link', { name: 'Dashboard' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  await test.step('shows the reports the API gives the administrator', async () => {
    await expect(page.getByText('Comptes', { exact: true })).toBeVisible();
    await expect(page.getByRole('link', { name: /Services/ })).toHaveAttribute(
      'href',
      '/admin/monitoring',
    );
  });
  await nav.getByRole('link', { name: 'Surveillance' }).click();
  await expect(page).toHaveURL(/\/admin\/monitoring$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Surveillance' })).toBeVisible();
  await expect(nav.getByRole('link', { name: 'Surveillance' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  expect(consoleErrors).toEqual([]);
});
