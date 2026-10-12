import type { Page } from '@playwright/test';
import { nodesOfType, readHead } from '../../support/head';
import { expect, test } from '../../support/worker-account';
import { createBuild } from '../builds-share/builds';

// A build saved through the real API, then ranked on the page the server renders from the
// trends' filters: the API's ranking and the server's page only meet on the stack. The filters
// and the rows are Vitest's, the ranking's rules the API's.

// A public build of the worker's account, created through the API, found through the filters.
test('ranks a public build under its filters', async ({
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

  await page.goto(`/en/trends?${query}`);

  await expect(row(page)).toContainText('Trending scroll');
  await expect(row(page)).toContainText(`By ${account.username}`);
  await expect(row(page).locator('.trend-row__chips')).toContainText('Arena');
  await expect(page.locator('lodb-trend-filters').getByLabel('Language')).toHaveValue('de_DE');
  const itemList = nodesOfType((await readHead(page)).jsonLd, 'ItemList');
  expect(JSON.stringify(itemList)).toContain(`/b/${build.shareToken}`);
});
