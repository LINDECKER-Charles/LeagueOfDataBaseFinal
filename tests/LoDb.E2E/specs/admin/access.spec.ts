import type { Browser, Page } from '@playwright/test';
import { expectNoAccessibilityViolations } from '../../support/accessibility';
import { enterPassword, LOGIN, signInAdmin } from './admin-account';
import { expect, NO_SESSION, openPanel, test } from './admin-test';

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
  'Journal',
];

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
  await expect(page.getByLabel(LOGIN.identifier)).toBeFocused();
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
  await expect(page).toHaveTitle('Connexion · Admin · LODB');
  await expect(page.locator('html')).toHaveAttribute('lang', 'fr');
  await expectNoAccessibilityViolations(page);
});

test('refuses credentials it does not know, without a second step', async ({
  browser,
  baseURL,
}) => {
  const page = await visitor(browser, baseURL);
  await page.goto('/admin/login');

  // An address no account has: no account is locked out by the attempt.
  await enterPassword(page, {
    email: `e2e_nobody_${Date.now().toString(36)}@example.com`,
    username: 'nobody',
    password: 'Not-the-password-2046!',
  });

  await expect(
    page.getByRole('alert').filter({ hasText: 'Identifiants invalides.' }),
  ).toBeVisible();
  await expect(page.getByLabel(LOGIN.code)).toHaveCount(0);
  // As the legacy page came back: the identifier kept and focused, the password emptied.
  await expect(page.getByLabel(LOGIN.identifier)).toBeFocused();
  await expect(page.getByLabel(LOGIN.identifier)).not.toHaveValue('');
  await expect(page.getByLabel(LOGIN.password, { exact: true })).toHaveValue('');
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
  await nav.getByRole('link', { name: 'Surveillance' }).click();
  await expect(page).toHaveURL(/\/admin\/monitoring$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Surveillance' })).toBeVisible();
  await expect(nav.getByRole('link', { name: 'Surveillance' })).toHaveAttribute(
    'aria-current',
    'page',
  );
  // The legacy URL of the journal still opens it, with the filters of the bookmark.
  await page.goto('/admin/logs?category=auth');
  await expect(page).toHaveURL(/\/admin\/journal\?category=auth$/);
  await expect(nav.getByRole('link', { name: 'Journal' })).toHaveAttribute('aria-current', 'page');
  await expect(page.locator('lodb-journal-filters').getByLabel(/^Catégorie/)).toHaveValue('auth');
  expect(consoleErrors).toEqual([]);
});
