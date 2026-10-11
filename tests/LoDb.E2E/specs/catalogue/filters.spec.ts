import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

// Any of the four lists would do: items has the most entries, so it always pages.
const LIST = '/en/items';
// FilterUrlSync coalesces URL rewrites over 300 ms; polling covers it.
const LIST_ROOT = 'lodb-catalogue-list';
const CARD = `${LIST_ROOT} .grid__cell`;
const RAIL_SEARCH = 'lodb-filter-console input[type=search]';
// The cards of the first page, which the server renders.
const FIRST_PAGE_SIZE = 12;

function queryOf(page: Page): URLSearchParams {
  return new URL(page.url()).searchParams;
}

// The first page is server-rendered; the whole list then loads for instant filtering.
async function openList(page: Page, url = LIST): Promise<void> {
  await page.goto(url);
  await page.waitForLoadState('networkidle');
}

// The browser's read of the whole list: the first page comes with the server's HTML.
function isWholeRead({ pathname, searchParams }: URL): boolean {
  return /^\/api\/catalog\/[^/]+\/[^/]+\/items$/.test(pathname) && !searchParams.has('page');
}

function wholeReadsOf(page: Page): string[] {
  const reads: string[] = [];
  page.on('request', (sent) => {
    if (isWholeRead(new URL(sent.url()))) {
      reads.push(sent.url());
    }
  });
  return reads;
}

// The highest count of the rail's options: drawn from the first page alone, never above it.
async function railTopCount(page: Page): Promise<number> {
  const counts = await page.locator('lodb-filter-console .chip__count').allTextContents();
  return Math.max(0, ...counts.map(Number));
}

test.describe('catalogue filters', { tag: '@readonly' }, () => {
  test('searches live and keeps the search in the URL, which a reload restores', async ({
    page,
    consoleErrors,
  }) => {
    const reads = wholeReadsOf(page);
    await openList(page);

    // The rail is hydrated late (on the reader's hand), but never left showing an outdated copy.
    await test.step('counts the whole list once it is read', async () => {
      await expect.poll(() => reads.length).toBe(1);
      await expect.poll(() => railTopCount(page)).toBeGreaterThan(FIRST_PAGE_SIZE);
    });

    await test.step('narrows by a facet, shows it as active, and clears everything', async () => {
      const before = [...queryOf(page).keys()];

      await page.locator('lodb-filter-console .chip:not([disabled])').first().click();

      await expect.poll(() => [...queryOf(page).keys()].length).toBeGreaterThan(before.length);
      await expect(page.locator('lodb-active-filters .active__chip')).toHaveCount(1);
      await page.locator('lodb-active-filters .clear').click();
      await expect.poll(() => new URL(page.url()).search).toBe('');
    });

    const total = await page.locator(CARD).count();

    await page.locator(RAIL_SEARCH).fill('boots');

    await expect.poll(() => queryOf(page).get('q')).toBe('boots');
    expect(await page.locator(CARD).count()).toBeLessThan(total);
    await page.reload();
    await expect(page.locator(RAIL_SEARCH)).toHaveValue('boots');
    await expect(page.locator(`${LIST_ROOT} lodb-active-filters`)).toBeVisible();
    expect(consoleErrors).toEqual([]);

    await test.step('empties its search when the results clear it', async () => {
      // Without the whole list, only the state moving can outdate the server's copy.
      await page.route(isWholeRead, (route) => route.abort());
      await page.goto(`${LIST}?q=boots`);
      await expect(page.locator(RAIL_SEARCH)).toHaveValue('boots');

      await page.locator('lodb-active-filters .clear').click();

      await expect.poll(() => queryOf(page).get('q')).toBeNull();
      await expect(page.locator(RAIL_SEARCH)).toHaveValue('');
    });
  });
});
