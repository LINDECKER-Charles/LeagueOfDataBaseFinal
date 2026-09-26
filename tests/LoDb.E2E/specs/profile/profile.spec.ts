import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';
import {
  deleteAccount,
  discardAccount,
  newAccount,
  register,
  type TestAccount,
} from '../account/accounts';

const CHAMPION = 'Ahri';

// Each change of the editor is saved on its own, a moment after it is made. The path is
// compared without the query: the editor sends `?version=` along.
function saved(page: Page, path: string) {
  return page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === path && response.request().method() === 'PUT',
  );
}

async function chooseChampion(page: Page): Promise<void> {
  await page.getByRole('button', { name: /^Champion — / }).click();
  const picker = page.getByRole('dialog');
  await picker.getByRole('searchbox').fill(CHAMPION);
  const save = saved(page, '/api/profile/favorites');
  await picker.locator('.picker-option__name', { hasText: new RegExp(`^${CHAMPION}$`) }).click();
  expect((await save).status()).toBe(200);
}

async function setPublic(page: Page, isPublic: boolean): Promise<void> {
  const save = saved(page, '/api/profile/visibility');
  await page.getByLabel('Profile visibility').setChecked(isPublic);
  expect((await save).ok()).toBe(true);
}

// The account of the journey, deleted even when a step fails: no run leaves one behind.
let created: TestAccount | undefined;

test.afterEach(async ({ playwright, baseURL }) => {
  if (created) {
    await discardAccount(playwright.request, baseURL, created);
    created = undefined;
  }
});

// The editor, the owner's preview and the page anyone reads, for one account.
test('makes a profile public, previews it and shows it to anyone', async ({ page, request }) => {
  const account = (created = newAccount('card'));
  const publicPath = `/en/u/${account.username}`;
  await register(page, account);

  await test.step('chooses a favorite champion, saved on its own', async () => {
    await chooseChampion(page);
    await expect(page.getByRole('button', { name: `Champion — ${CHAMPION}` })).toBeVisible();
  });

  await test.step('makes the profile public, with a link to its page', async () => {
    await setPublic(page, true);
    // The link reads as the legacy one did, `/u/{name}`, and leads to the page's locale.
    const link = page.getByRole('link', { name: `/u/${account.username}` });
    await expect(link).toHaveAttribute('href', publicPath);
  });

  await test.step('previews the card as others will see it', async () => {
    await page.getByRole('link', { name: 'Preview public profile' }).click();
    await expect(page).toHaveURL(/\/en\/account\/profile\/preview$/);
    await expect(
      page.getByText('This is exactly how your public profile appears to others.'),
    ).toBeVisible();
    await expect(page.getByRole('heading', { level: 1 })).toContainText(account.username);
    await page.getByRole('link', { name: 'Back to editor' }).click();
  });

  await test.step('renders the public card on the server, for anyone', async () => {
    const response = await request.get(publicPath);
    const html = await response.text();

    expect(response.status()).toBe(200);
    expect(html).toContain('ng-server-context="ssr"');
    expect(html).toContain('"@type":"ProfilePage"');
    expect(html).toContain(`${publicPath}"`);
    expect(html).toContain(CHAMPION);
    expect(response.headers()['x-robots-tag'] ?? '').not.toContain('noindex');
  });

  await test.step('hides it again once private', async () => {
    await page.goto('/en/account/profile');
    await setPublic(page, false);
    expect((await request.get(`/api/profiles/${account.username}`)).status()).toBe(404);
    // nginx keeps the public copy for its s-maxage: another address asks the server again.
    const page404 = await request.get(`${publicPath}?e2e=private`);
    expect(page404.status()).toBe(404);
    expect(page404.headers()['x-robots-tag']).toContain('noindex');
  });

  await test.step('deletes the account', async () => {
    await deleteAccount(page, account);
  });
});

test('answers an unknown summoner with a 404 kept out of the index', async ({ page }) => {
  const response = await page.goto('/en/u/nobody-e2e-at-all');

  expect(response?.status()).toBe(404);
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
  await expect(page.locator('link[rel="canonical"]')).toHaveCount(0);
});
