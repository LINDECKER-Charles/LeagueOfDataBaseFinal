import type { APIRequestContext } from '@playwright/test';
import { type DetailNeighbours, expectServedDetail } from '../../support/catalog';
import { expect, test } from '../../support/test';

interface Meta {
  readonly latest: string | null;
}

interface Item {
  readonly canonicalPath: string;
  readonly profile: { readonly name: string };
  readonly neighbours: DetailNeighbours;
}

const LIST = '/en/items';
const CARD = 'lodb-catalogue-list .grid__cell';
// Infinity Edge is built from other items.
const CRAFTED = 3031;

// The item as /en/ shows it: the latest version, in en_US.
async function itemOf(request: APIRequestContext, id: number): Promise<Item> {
  const meta = (await (await request.get('/api/meta')).json()) as Meta;
  expect(meta.latest, 'the stack must have ingested a version').not.toBeNull();
  const response = await request.get(`/api/catalog/${meta.latest}/en_US/items/${id}`);
  expect(response.status()).toBe(200);
  return (await response.json()) as Item;
}

test.describe('item pages as crawlers read them', { tag: '@readonly' }, () => {
  test.use({ javaScriptEnabled: false });

  test('renders an item with its recipe tree', async ({ page, request }) => {
    const item = await itemOf(request, CRAFTED);

    const response = await page.goto(`/en/${item.canonicalPath}`);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText(item.profile.name);
    await expectServedDetail(page, response, item.neighbours);
    await expect(page.locator('lodb-recipe-tree a.recipe-node').first()).toBeAttached();
    await expect(page.locator('script[type="application/ld+json"]').first()).toBeAttached();
    await expect(page.locator('link[rel="canonical"]')).toHaveAttribute(
      'href',
      new RegExp(`/en/${item.canonicalPath}$`),
    );
  });
});

test.describe('item pages', { tag: '@readonly' }, () => {
  test('lead from a card to its item, then back to the list', async ({ page, consoleErrors }) => {
    await page.goto(LIST);
    await page.waitForLoadState('networkidle');

    await page.locator(`${CARD} a`).first().click();

    await expect(page).toHaveURL(/\/en\/items\/.+/);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.locator('lodb-pager a[rel="next"], lodb-pager a[rel="prev"]').first().waitFor();
    await page.locator('lodb-pager .pager__hub').click();
    await expect(page).toHaveURL(/\/en\/items$/);
    expect(consoleErrors).toEqual([]);
  });
});
