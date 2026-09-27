import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

// Any of the four lists would do: items has the most entries, so it always pages.
const LIST = '/en/items';
// FilterUrlSync coalesces URL rewrites over 300 ms; polling covers it.
const LIST_ROOT = 'lodb-catalogue-list';
const CARD = `${LIST_ROOT} .grid__cell`;
const RAIL_SEARCH = 'lodb-filter-console input[type=search]';
const MOBILE = { width: 390, height: 844 };
const SHORT_PHONE = { width: 360, height: 560 };

function queryOf(page: Page): URLSearchParams {
  return new URL(page.url()).searchParams;
}

// The first page is server-rendered; the whole list then loads for instant filtering.
async function openList(page: Page, url = LIST): Promise<void> {
  await page.goto(url);
  await page.waitForLoadState('networkidle');
}

test.describe('catalogue list as crawlers read it', { tag: '@readonly' }, () => {
  test.use({ javaScriptEnabled: false });

  test('renders the first page and links the next one', async ({ page }) => {
    await page.goto(LIST);

    expect(await page.locator(CARD).count()).toBeGreaterThan(0);
    await expect(page.locator(`${LIST_ROOT} a[rel="next"]`)).toHaveAttribute(
      'href',
      `${LIST}?page=2`,
    );
  });
});

test.describe('catalogue filters', { tag: '@readonly' }, () => {
  test('searches live and keeps the search in the URL, which a reload restores', async ({
    page,
    consoleErrors,
  }) => {
    await openList(page);
    const total = await page.locator(CARD).count();

    await page.locator(RAIL_SEARCH).fill('boots');

    await expect.poll(() => queryOf(page).get('q')).toBe('boots');
    expect(await page.locator(CARD).count()).toBeLessThan(total);
    await page.reload();
    await expect(page.locator(RAIL_SEARCH)).toHaveValue('boots');
    await expect(page.locator(`${LIST_ROOT} lodb-active-filters`)).toBeVisible();
    expect(consoleErrors).toEqual([]);
  });

  test('narrows by a facet value, shows it as active, and clears everything', async ({ page }) => {
    await openList(page);
    const before = [...queryOf(page).keys()];

    await page.locator('lodb-filter-console .chip:not([disabled])').first().click();

    await expect.poll(() => [...queryOf(page).keys()].length).toBeGreaterThan(before.length);
    await expect(page.locator('lodb-active-filters .active__chip')).toHaveCount(1);
    await page.locator('lodb-active-filters .clear').click();
    await expect.poll(() => new URL(page.url()).search).toBe('');
  });

  test('pages and resizes through the URL, and keeps foreign parameters', async ({ page }) => {
    await openList(page, `${LIST}?lang=en_GB`);

    await page.locator(`${LIST_ROOT} a[rel="next"]`).click();
    await expect.poll(() => queryOf(page).get('page')).toBe('2');
    await page.locator('lodb-filter-toolbar .segment').last().click();

    await expect.poll(() => queryOf(page).get('size')).toBe('all');
    expect(queryOf(page).get('page')).toBeNull();
    expect(queryOf(page).get('lang')).toBe('en_GB');
  });

  test('jumps to the search on `/`', async ({ page }) => {
    await openList(page);

    await page.locator('body').press('/');

    await expect(page.locator(RAIL_SEARCH)).toBeFocused();
  });

  test('offers the facets in a bottom sheet on a phone', async ({ page }) => {
    await page.setViewportSize(MOBILE);
    await openList(page);

    await page.locator(`${LIST_ROOT} .bar__trigger`).click();

    const sheet = page.getByRole('dialog');
    await expect(sheet).toBeVisible();
    await sheet.locator('.chip:not([disabled])').first().click();
    await sheet.locator('.sheet__done').click();
    await expect(sheet).toBeHidden();
    await expect(page.locator('lodb-active-filters .active__chip')).toHaveCount(1);
  });

  // A short phone makes the facets outgrow the sheet: its panel must scroll, footer in view.
  test('keeps the sheet footer on screen when the facets outgrow it', async ({ page }) => {
    await page.setViewportSize(SHORT_PHONE);
    await openList(page);

    await page.locator(`${LIST_ROOT} .bar__trigger`).click();

    const sheet = page.getByRole('dialog');
    const panel = sheet.locator('.hx-dialog-panel');
    await expect(panel).toBeVisible();
    const overflow = await panel.evaluate((node) => node.scrollHeight - node.clientHeight);
    expect(overflow).toBeGreaterThan(0);
    const box = await panel.boundingBox();
    expect(box?.height ?? Infinity).toBeLessThanOrEqual(SHORT_PHONE.height);
    await expect(sheet.locator('.sheet__done')).toBeInViewport();
    await panel.evaluate((node) => node.scrollTo({ top: node.scrollHeight }));
    await sheet.locator('.sheet__done').click();
    await expect(sheet).toBeHidden();
  });
});
