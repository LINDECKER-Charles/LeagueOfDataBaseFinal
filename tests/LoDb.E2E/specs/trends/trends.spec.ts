import type { Page } from '@playwright/test';
import { nodesOfType, readHead } from '../../support/head';
import { expect, test } from '../../support/worker-account';
import { createBuild, voteSaved } from '../builds-share/builds';

function filter(page: Page, label: string) {
  return page.locator('lodb-trend-filters').getByLabel(label);
}

// Locally the absolute URLs lose the stack's port (nginx forwards `Host: $host`), and the
// canonical origin is the deployment's setting: the path is what the page decides.
function pathOf(url: string): string {
  const parsed = new URL(url);
  return `${parsed.pathname}${parsed.search}${parsed.hash}`;
}

test.describe('the trends, without an account', { tag: '@readonly' }, () => {
  test('renders an indexable page, canonical without its query', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL, javaScriptEnabled: false });
    const page = await context.newPage();

    const response = await page.goto('/en/trends?mode=aram&page=2');
    const head = await readHead(page);

    expect(response?.status()).toBe(200);
    expect(response?.headers()['x-robots-tag'] ?? '').not.toContain('noindex');
    expect(head.robots ?? '').not.toContain('noindex');
    expect(head.canonicals.map(pathOf)).toEqual(['/en/trends']);
    const [trail] = nodesOfType(head.jsonLd, 'BreadcrumbList');
    expect(trail?.['itemListElement']).toHaveLength(2);
    await expect(page.locator('h1')).toHaveText('Trending builds');
    await context.close();
  });

  test('keeps its filters in the URL, and starts again at the first page', async ({
    page,
    consoleErrors,
  }) => {
    await page.goto('/en/trends?page=2');
    await filter(page, 'Game mode').selectOption('aram');
    await page.getByRole('button', { name: 'Filter' }).click();

    await expect(page).toHaveURL(/\/en\/trends\?mode=aram$/);
    await expect(filter(page, 'Game mode')).toHaveValue('aram');
    await page.reload();
    await expect(filter(page, 'Game mode')).toHaveValue('aram');

    await filter(page, 'Game mode').selectOption('');
    await page.getByRole('button', { name: 'Filter' }).click();
    await expect(page).toHaveURL(/\/en\/trends$/);
    expect(consoleErrors).toEqual([]);
  });

  test('filters without scripts, through the form', async ({ browser, baseURL }) => {
    const context = await browser.newContext({ baseURL, javaScriptEnabled: false });
    const page = await context.newPage();

    await page.goto('/en/trends');
    await filter(page, 'Game mode').selectOption('arena');
    await page.getByRole('button', { name: 'Filter' }).click();

    await expect(page).toHaveURL(/[?&]mode=arena(&|$)/);
    await expect(filter(page, 'Game mode')).toHaveValue('arena');
    await context.close();
  });

  test('pages through the ranking, keeping the filters', async ({ page, request }) => {
    const first = (await (await request.get('/api/trends')).json()) as { pages: number };
    test.skip(first.pages < 2, 'the stack holds a single page of public builds');

    await page.goto('/en/trends');
    await page.locator('.trends-pager a[rel="next"]').click();

    await expect(page).toHaveURL(/\/en\/trends\?page=2$/);
    await expect(page.locator('.trends-pager__position')).toHaveText(`page 2 / ${first.pages}`);
    await page.locator('.trends-pager a[rel="prev"]').click();
    await expect(page).toHaveURL(/\/en\/trends$/);
  });
});

// A public build of the worker's account, found through the filters, then voted on.
test('ranks a public build, offers to forge one and takes votes', async ({
  page,
  member: owner,
  workerAccount: account,
}) => {
  test.slow();
  const build = await createBuild(owner, {
    name: 'Trending scroll',
    isPublic: true,
    language: 'de_DE',
    gameMode: 'arena',
  });
  // A query of its own, which no proxy has cached yet.
  const query = `champion=${build.championId}&mode=arena&language=de_DE&run=${build.shareToken}`;
  const row = (reader: Page) =>
    reader.locator('lodb-trend-row', { has: reader.locator(`a[href="/b/${build.shareToken}"]`) });

  await test.step('lists it under its filters, with its author and chips', async () => {
    await page.goto(`/en/trends?${query}`);
    await expect(row(page)).toContainText('Trending scroll');
    await expect(row(page)).toContainText(`By ${account.username}`);
    await expect(row(page).locator('.trend-row__chips')).toContainText('Arena');
    await expect(filter(page, 'Language')).toHaveValue('de_DE');
    const itemList = nodesOfType((await readHead(page)).jsonLd, 'ItemList');
    expect(JSON.stringify(itemList)).toContain(`/b/${build.shareToken}`);
  });

  await test.step('offers the sign-up to a visitor, the editor to a signed-in reader', async () => {
    await expect(page.getByRole('link', { name: 'Create account' }).first()).toHaveAttribute(
      'href',
      '/en/account/register',
    );
    await owner.goto(`/en/trends?${query}`);
    await expect(owner.getByRole('link', { name: 'Forge my build' }).first()).toHaveAttribute(
      'href',
      '/en/account/builds/new',
    );
  });

  await test.step('takes the reader’s vote on the row', async () => {
    const up = row(owner).getByRole('button', { name: 'Upvote this build' });
    const voted = voteSaved(owner, build);
    await up.click();
    await expect(row(owner).locator('.vote-score')).toHaveText('+1');
    // The row shows the vote before the API has it: the reload waits for its answer.
    expect((await voted).ok(), 'the API keeps the vote').toBe(true);
    await owner.reload();
    await expect(up).toHaveAttribute('aria-pressed', 'true');
    const withdrawn = voteSaved(owner, build);
    await up.click();
    await expect(row(owner).locator('.vote-score')).toHaveText('0');
    expect((await withdrawn).ok(), 'the API withdraws the vote').toBe(true);
  });
});
