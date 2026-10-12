import type { Browser, Page } from '@playwright/test';
import { createMember, signInMember } from '../../support/member-account';
import { discardAccount } from '../account/accounts';
import { signInAdmin } from './admin-account';
import { anonymous, expect, NO_SESSION, openPanel, test } from './admin-test';

// Every panel of the navigation, in its order: analytics, then management.
const PANELS = [
  "Vue d'ensemble",
  'Trafic',
  'Audience',
  'Stockage',
  'Utilisateurs',
  'Builds',
  'Dons',
  'Messages',
  'Clients API',
  'Surveillance',
  'Sauvegardes',
  'Journal',
];
// The sections of the overview, each drawn from the API.
const OVERVIEW_SECTIONS = [
  'Fréquentation',
  'Par ressource',
  'Pages les plus consultées',
  'Sources de trafic',
  "Familles d'objets",
  'Déduplication',
  'Santé',
];
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

test('lists every panel in its navigation and opens on the overview', async ({
  page,
  consoleErrors,
}) => {
  await openPanel(page, '/admin', "Vue d'ensemble");

  const nav = page.getByRole('navigation', { name: "Navigation de l'administration" });
  await expect(nav.getByRole('link')).toHaveText(PANELS);
  await expect(nav.getByRole('link', { name: "Vue d'ensemble" })).toHaveAttribute(
    'aria-current',
    'page',
  );
  await test.step('shows the overview: the application, its traffic and its storage', async () => {
    await expect(page).toHaveTitle("Vue d'ensemble · Admin · LODB");
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
    for (const heading of OVERVIEW_SECTIONS) {
      await expect(page.getByRole('heading', { level: 2, name: heading })).toBeVisible();
    }
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
