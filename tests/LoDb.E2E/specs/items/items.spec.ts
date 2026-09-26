import type { APIRequestContext } from '@playwright/test';
import { expect, test } from '../../support/test';

interface Meta {
  readonly latest: string | null;
}

interface Link {
  readonly canonicalPath?: string | null;
  readonly edition: 'modern' | 'classic';
  readonly name: string;
}

interface Item {
  readonly canonicalPath: string;
  readonly profile: { readonly name: string; readonly counterpart?: Link | null };
  readonly recipe?: { readonly components: readonly { readonly canonicalPath: string }[] } | null;
}

const LIST = '/en/items';
const CARD = 'lodb-catalogue-list .grid__cell';
// Infinity Edge is built from other items; Faerie Charm has a LoL Classic twin.
const CRAFTED = 3031;
const TWINNED = 1004;

// The item as /en/ shows it: the latest version, in en_US.
async function itemOf(request: APIRequestContext, id: number): Promise<Item> {
  const meta = (await (await request.get('/api/meta')).json()) as Meta;
  expect(meta.latest, 'the stack must have ingested a version').not.toBeNull();
  const response = await request.get(`/api/catalog/${meta.latest}/en_US/items/${id}`);
  expect(response.status()).toBe(200);
  return (await response.json()) as Item;
}

test.describe('item pages as crawlers read them', () => {
  test.use({ javaScriptEnabled: false });

  test('renders the list, each card linking its item page', async ({ page }) => {
    await page.goto(LIST);

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(await page.locator(CARD).count()).toBeGreaterThan(0);
    await expect(page.locator(`${CARD} a`).first()).toHaveAttribute('href', /^\/en\/items\//);
  });

  test('renders an item with its recipe tree, each component linking its page', async ({
    page,
    request,
  }) => {
    const item = await itemOf(request, CRAFTED);

    await page.goto(`/en/${item.canonicalPath}`);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText(item.profile.name);
    const links = page.locator('lodb-recipe-tree a.recipe-node');
    for (const component of item.recipe?.components ?? []) {
      await expect(links.and(page.locator(`[href="/en/${component.canonicalPath}"]`))).toHaveCount(
        1,
      );
    }
    await expect(page.locator('script[type="application/ld+json"]').first()).toBeAttached();
    await expect(page.locator('link[rel="canonical"]')).toHaveAttribute(
      'href',
      new RegExp(`/en/${item.canonicalPath}$`),
    );
  });
});

test.describe('item pages', () => {
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

  test('link an item to its LoL Classic twin, which marks its edition', async ({
    page,
    request,
    consoleErrors,
  }) => {
    const item = await itemOf(request, TWINNED);
    const twin = item.profile.counterpart;
    test.skip(!twin?.canonicalPath, 'the stack carries no LoL Classic edition');

    await page.goto(`/en/${item.canonicalPath}`);
    const link = page.locator('lodb-edition-counterpart a[data-edition="classic"]');
    await expect(link).toHaveAttribute('href', `/en/${twin?.canonicalPath}`);
    await link.click();

    await expect(page).toHaveURL(new RegExp(`/en/${twin?.canonicalPath}$`));
    await expect(page.locator('header lodb-edition-badge')).toContainText('LoL Classic');
    await expect(page.locator('lodb-edition-counterpart a[data-edition="modern"]')).toHaveAttribute(
      'href',
      `/en/${item.canonicalPath}`,
    );
    expect(consoleErrors).toEqual([]);
  });
});
