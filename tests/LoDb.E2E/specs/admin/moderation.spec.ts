import type { BrowserContext, Page } from '@playwright/test';
import { createMember, signInMember } from '../../support/member-account';
import { discardAccount, type TestAccount } from '../account/accounts';
import { createBuild } from '../builds-share/builds';
import {
  anonymous,
  confirmAction,
  expect,
  expectToast,
  NO_SESSION,
  openPanel,
  test,
} from './admin-test';

/** The member the administrator moderates, signed in on a browser of their own. */
interface Member {
  readonly account: TestAccount;
  readonly context: BrowserContext;
  readonly page: Page;
  readonly buildName: string;
}

// One member for the whole journey, whose steps follow one another.
test.describe.configure({ mode: 'serial' });

// A verified account, and its public build: the CLI, the sign-in, then the API.
const SETUP_TIMEOUT_MS = 90_000;
const BAN_REASON = 'Builds en double (suite de bout en bout)';

let member: Member | undefined;

function joined(): Member {
  if (member === undefined) {
    throw new Error('the member of the journey was not created');
  }
  return member;
}

test.beforeAll(async ({ browser }, testInfo) => {
  test.setTimeout(SETUP_TIMEOUT_MS);
  // Created by the CLI: the registration form's quota is account/register.spec.ts's.
  const account = createMember('admmod', { verified: true });
  // The member's own browser, and the clean-up's requests: without an empty state, they would
  // carry the session of the administrator.
  const context = await browser.newContext({
    baseURL: testInfo.project.use.baseURL,
    storageState: NO_SESSION,
  });
  const page = await context.newPage();
  member = { account, context, page, buildName: `E2E moderation ${Date.now().toString(36)}` };
  await signInMember(page, testInfo.project.use.baseURL, account);
  await createBuild(page, {
    name: member.buildName,
    isPublic: true,
    language: 'fr_FR',
    gameMode: 'aram',
  });
});

// Deleted by the journey; a failed step leaves it to the clean-up.
test.afterAll(async ({ playwright }, testInfo) => {
  if (member !== undefined) {
    await discardAccount(
      anonymous(playwright.request),
      testInfo.project.use.baseURL,
      member.account,
    );
    await member.context.close();
    member = undefined;
  }
});

test('hides the admin from a member, behind the 404 of the URL', async ({ request }) => {
  const { page } = joined();
  const copy = (await (await request.get('/i18n/seo/en.json')).json()) as {
    error: { '404': { title: string } };
  };

  await page.goto('/admin/users');

  await expect(page).toHaveURL(/\/admin\/users$/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(copy.error['404'].title);
  await expect(
    page.getByRole('navigation', { name: "Navigation de l'administration" }),
  ).toHaveCount(0);
});

test('finds a build by its name, public, with its author', async ({ page }) => {
  const { account, buildName } = joined();
  await openPanel(page, '/admin/builds', 'Builds');

  await page.getByLabel('Nom, champion ou auteur').fill(buildName);
  await page.getByLabel('Visibilité').selectOption({ label: 'Public' });
  await page.getByRole('button', { name: 'Filtrer' }).click();

  await expect(page).toHaveURL(/[?&]visibility=public/);
  const row = page.locator('tr[data-build]', { hasText: buildName });
  await expect(row).toHaveCount(1);
  await expect(row.getByRole('link', { name: account.username })).toBeVisible();
  await expect(row.getByText('Public', { exact: true })).toBeVisible();
});

test('unpublishes a public build', async ({ page }) => {
  const { buildName } = joined();
  await openPanel(page, `/admin/builds?q=${encodeURIComponent(buildName)}`, 'Builds');
  const row = page.locator('tr[data-build]', { hasText: buildName });

  await confirmAction(row, 'Dépublier', 'Confirmer la dépublication');

  await expectToast(page, `« ${buildName} » n'est plus public.`);
  await expect(row.getByText('Privé', { exact: true })).toBeVisible();
  await expect(row.getByRole('button', { name: 'Dépublier' })).toHaveCount(0);
});

test('deletes a build', async ({ page }) => {
  const { buildName } = joined();
  await openPanel(page, `/admin/builds?q=${encodeURIComponent(buildName)}`, 'Builds');
  const row = page.locator('tr[data-build]', { hasText: buildName });

  await confirmAction(row, 'Supprimer', 'Supprimer définitivement');

  await expectToast(page, `« ${buildName} » est supprimé.`);
  await expect(page.getByText('Aucun build ne correspond.')).toBeVisible();
});

test('finds an account, then opens its activity', async ({ page }) => {
  const { account } = joined();
  await openPanel(page, '/admin/users', 'Utilisateurs');

  await page.getByLabel("Nom d'utilisateur ou e-mail").fill(account.username);
  await page.getByRole('button', { name: 'Rechercher' }).click();

  await expect(page).toHaveURL(/[?&]q=/);
  await expect(page.getByText('1 résultat', { exact: true })).toBeVisible();
  const row = page.locator('tr[data-user]', { hasText: account.username });
  await expect(row).toContainText(account.email);
  await row.getByRole('link', { name: 'Activité' }).click();
  await expect(page).toHaveURL(/\/admin\/users\/\d+\/activity$/);
  await expect(
    page.getByRole('heading', { level: 1, name: `Activité de ${account.username}` }),
  ).toBeVisible();
  // The member's sign-in: created by the CLI, the account has no registration to journal.
  await expect(
    page.getByRole('table').getByText('Connexion', { exact: true }).first(),
  ).toBeVisible();
  await page.getByRole('link', { name: 'Retour aux utilisateurs' }).click();
  await expect(page).toHaveURL(/\/admin\/users(\?|$)/);
});

test('bans an account, with its reason', async ({ page }) => {
  const { account } = joined();
  await openPanel(page, `/admin/users?q=${account.username}`, 'Utilisateurs');
  const row = page.locator('tr[data-user]', { hasText: account.username });

  await row.getByRole('button', { name: 'Bannir', exact: true }).click();
  await page
    .getByLabel(`Motif du bannissement de ${account.username} (facultatif)`)
    .fill(BAN_REASON);
  await page.getByRole('button', { name: 'Bannir le compte' }).click();

  await expectToast(page, `${account.username} est banni.`);
  await expect(row.getByText('Banni', { exact: true })).toHaveAttribute('title', BAN_REASON);
  await expect(row.getByRole('button', { name: 'Rétablir' })).toBeVisible();
});

test('lifts the ban of an account', async ({ page }) => {
  const { account } = joined();
  await openPanel(page, `/admin/users?q=${account.username}`, 'Utilisateurs');
  const row = page.locator('tr[data-user]', { hasText: account.username });

  await confirmAction(row, 'Rétablir', 'Confirmer le rétablissement');

  await expectToast(page, `${account.username} est rétabli.`);
  await expect(row.getByText('Banni', { exact: true })).toHaveCount(0);
  await expect(row.getByRole('button', { name: 'Bannir', exact: true })).toBeVisible();
});

test('deletes an account', async ({ page }) => {
  const { account } = joined();
  await openPanel(page, `/admin/users?q=${account.username}`, 'Utilisateurs');
  const row = page.locator('tr[data-user]', { hasText: account.username });

  await confirmAction(row, 'Supprimer', 'Supprimer définitivement');

  await expectToast(page, `Le compte ${account.username} est supprimé.`);
  await expect(page.getByText('Aucun compte ne correspond.')).toBeVisible();
});

test('journals every moderation under the administrator', async ({ page, admin }) => {
  const actor = encodeURIComponent(admin.username);
  await openPanel(page, `/admin/journal?category=admin&actor=${actor}`, 'Journal');

  // In the table: the filters list the same actions among their choices.
  const journal = page.getByRole('table');
  for (const action of [
    'Build dépublié',
    'Build supprimé (modération)',
    'Bannissement de compte',
    'Rétablissement de compte',
    'Suppression de compte',
  ]) {
    await expect(journal.getByText(action, { exact: true }).first()).toBeVisible();
  }
});
