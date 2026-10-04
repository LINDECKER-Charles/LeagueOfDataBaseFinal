import type { Browser, Page } from '@playwright/test';
import { readHead, typesOf } from '../../support/head';
import { expect, test } from '../../support/worker-account';
import { createBuild, voteSaved } from './builds';

// The build is written in French: its page speaks it, unless `?lang=` names another language,
// the one the visitor picked in the switcher (as the legacy session did).
const LANGUAGE = 'fr_FR';

async function newVisitor(browser: Browser, baseURL: string | undefined, javaScriptEnabled = true) {
  const context = await browser.newContext({ baseURL, javaScriptEnabled });
  return context.newPage();
}

function voteScore(page: Page) {
  return page.locator('.bshare-head .vote-score');
}

// The owner, the worker's account, and a visitor, around one public build.
test('shares a public build, unlisted, in its own language, with its score', async ({
  page,
  member: owner,
  workerAccount: account,
  request,
  browser,
  baseURL,
  consoleErrors,
}) => {
  test.slow();
  const build = await createBuild(owner, {
    name: 'Shared scroll',
    isPublic: true,
    language: LANGUAGE,
    gameMode: 'aram',
  });
  const path = `/b/${build.shareToken}`;

  await test.step('serves an unlisted page without structured data', async () => {
    const response = await request.get(path);
    expect(response.status()).toBe(200);
    expect(response.headers()['x-robots-tag']).toContain('noindex');
    const crawler = await newVisitor(browser, baseURL, false);
    await crawler.goto(path);
    const head = await readHead(crawler);
    expect(head.robots).toContain('noindex');
    expect(head.canonicals).toEqual([]);
    expect(typesOf(head.jsonLd).size).toBe(0);
    expect(head.title).toContain('Shared scroll');
    await expect(crawler.locator('html')).toHaveAttribute('lang', 'fr');
    await crawler.context().close();
  });

  await test.step('speaks the language ?lang= names, the build’s own without one', async () => {
    await page.goto(`${path}?lang=en_US`);
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.locator('.bshare-head h1')).toHaveText('Shared scroll');
    await expect(page.locator('[data-mode]')).toHaveText('ARAM');
    await expect(page.locator('[data-version]')).toContainText(build.gameVersion);
    await expect(page.locator('.bshare-head')).toContainText(`By ${account.username}`);
    await expect(page.locator('.bsteps-node .bshare-item')).toHaveCount(2);
  });

  await test.step('sends a visitor to sign in before voting', async () => {
    await page.goto(path);
    await expect(voteScore(page)).toHaveText('0');
    const login = page.getByRole('link', { name: 'Connectez-vous pour voter' }).first();
    await expect(login).toHaveAttribute(
      'href',
      `/fr/account/login?returnUrl=${encodeURIComponent(path)}`,
    );
  });

  await test.step('copies the link of the page', async () => {
    await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.getByRole('button', { name: 'Copier le lien' }).click();
    await expect(page.getByRole('button', { name: 'Copié !' })).toBeVisible();
    const copied = await page.evaluate(() => navigator.clipboard.readText());
    expect(copied).toBe(`${new URL(page.url()).origin}${path}`);
  });

  await test.step('lets a signed-in reader vote, keeps the vote, and withdraws it', async () => {
    await owner.goto(path);
    const up = owner.getByRole('button', { name: 'Voter pour ce build' });
    const voted = voteSaved(owner, build);
    await up.click();
    await expect(voteScore(owner)).toHaveText('+1');
    await expect(up).toHaveAttribute('aria-pressed', 'true');
    expect((await voted).ok(), 'the API keeps the vote').toBe(true);
    // The server renders the score for nobody; the reader's own vote comes back after it.
    await owner.reload();
    await expect(up).toHaveAttribute('aria-pressed', 'true');
    await expect(voteScore(owner)).toHaveText('+1');
    const withdrawn = voteSaved(owner, build);
    await up.click();
    await expect(voteScore(owner)).toHaveText('0');
    await expect(up).toHaveAttribute('aria-pressed', 'false');
    expect((await withdrawn).ok(), 'the API withdraws the vote').toBe(true);
  });

  expect(consoleErrors).toEqual([]);
});
