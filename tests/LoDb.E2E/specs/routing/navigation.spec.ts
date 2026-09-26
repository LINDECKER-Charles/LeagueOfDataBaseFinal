import type { APIRequestContext, Page } from '@playwright/test';
import { expect, test } from '../../support/test';

interface Meta {
  readonly latest: string | null;
  readonly versions: readonly string[];
}

interface Item {
  readonly canonicalPath: string;
  readonly profile: { readonly name: string };
}

const ITEM = 1036;
const MARK = 'lodbSameDocument';

async function metaOf(request: APIRequestContext): Promise<Meta & { readonly latest: string }> {
  const meta = (await (await request.get('/api/meta')).json()) as Meta;
  expect(meta.latest, 'the stack must have ingested a version').not.toBeNull();
  return { ...meta, latest: meta.latest ?? '' };
}

// The item as /en/ shows it: canonical path and name in the latest version, in en_US.
async function latestItem(request: APIRequestContext): Promise<Item> {
  const { latest } = await metaOf(request);
  const response = await request.get(`/api/catalog/${latest}/en_US/items/${ITEM}`);
  return (await response.json()) as Item;
}

// Marks the document: still marked later, the page changed through the router, not a load.
async function markDocument(page: Page): Promise<void> {
  await page.waitForLoadState('networkidle');
  await page.evaluate((mark) => Reflect.set(window, mark, true), MARK);
}

async function isSameDocument(page: Page): Promise<boolean> {
  return page.evaluate((mark) => Reflect.get(window, mark) === true, MARK);
}

function pathOf(page: Page): string {
  return new URL(page.url()).pathname;
}

test.describe('navigation', () => {
  test('goes from a prerendered page to another through the router', async ({
    page,
    consoleErrors,
  }) => {
    await page.goto('/en/about');
    await markDocument(page);

    await page.locator('footer a[href="/en/faq"]').click();

    await expect.poll(() => pathOf(page)).toBe('/en/faq');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(await isSameDocument(page)).toBe(true);
    expect(consoleErrors).toEqual([]);
  });

  test('follows the 301 of an item without its slug to the canonical URL', async ({
    page,
    request,
  }) => {
    const item = await latestItem(request);

    const response = await page.goto(`/en/items/${ITEM}`);

    expect(pathOf(page)).toBe(`/en/${item.canonicalPath}`);
    expect(response?.status()).toBe(200);
    expect((await response?.request().redirectedFrom()?.response())?.status()).toBe(301);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(item.profile.name);
  });

  test('corrects a stale history entry in the browser, without reloading', async ({
    page,
    request,
    consoleErrors,
  }) => {
    const item = await latestItem(request);
    await page.goto('/en/about');
    await markDocument(page);

    await page.evaluate((path) => {
      history.pushState(null, '', path);
      dispatchEvent(new PopStateEvent('popstate', { state: null }));
    }, `/en/items/${ITEM}`);

    await expect.poll(() => pathOf(page)).toBe(`/en/${item.canonicalPath}`);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(item.profile.name);
    expect(await isSameDocument(page)).toBe(true);
    expect(consoleErrors).toEqual([]);
  });

  test('serves an older version pinned in the path, cached for a week', async ({
    page,
    request,
  }) => {
    const { latest, versions } = await metaOf(request);
    const older = versions[versions.indexOf(latest) + 1] ?? '';

    const response = await page.goto(`/en/${older}/champions`);

    expect(response?.status()).toBe(200);
    expect(response?.headers()['cache-control']).toBe('public, max-age=3600, s-maxage=604800');
    await expect(page.getByRole('main').getByText(older)).toBeVisible();
  });

  test('renders a private page in the browser, never stored nor indexed', async ({
    page,
    consoleErrors,
  }) => {
    const response = await page.goto('/en/account/login');

    expect(response?.status()).toBe(200);
    expect(response?.headers()['cache-control']).toBe('private, no-store');
    expect(response?.headers()['x-robots-tag']).toBe('noindex');
    expect(await response?.text()).not.toContain('ng-server-context="ssr"');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.waitForLoadState('networkidle');
    expect(consoleErrors).toEqual([]);
  });
});
