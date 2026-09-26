import type { APIRequestContext } from '@playwright/test';
import { expect, test } from '../../support/test';

interface Meta {
  readonly latest: string | null;
}

interface RuneList {
  readonly entries: readonly { readonly key: string; readonly canonicalPath: string }[];
  readonly trees: readonly { readonly name: string; readonly canonicalPath: string }[];
}

const LIST = '/en/runes';
const CARD = 'lodb-catalogue-list lodb-rune-card';
const RUNE_PATHS = 5;
// A path strings its keystones over three rows of at least three minor runes.
const MINOR_RUNES = 9;

// The rune list as /en/ shows it: the latest version, in en_US.
async function runesOf(request: APIRequestContext): Promise<RuneList> {
  const meta = (await (await request.get('/api/meta')).json()) as Meta;
  expect(meta.latest, 'the stack must have ingested a version').not.toBeNull();
  const response = await request.get(`/api/catalog/${meta.latest}/en_US/runes?page=1&size=1`);
  expect(response.status()).toBe(200);
  return (await response.json()) as RuneList;
}

test.describe('rune pages as crawlers read them', () => {
  test.use({ javaScriptEnabled: false });

  test('renders the band of paths and the runes, each linking its card on its path', async ({
    page,
  }) => {
    await page.goto(LIST);

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(page.locator('nav a[href^="/en/runes/"]')).toHaveCount(RUNE_PATHS);
    expect(await page.locator(CARD).count()).toBeGreaterThan(0);
    await expect(page.locator(`${CARD} a`).first()).toHaveAttribute(
      'href',
      /^\/en\/runes\/[^#?]+#rune-\w+$/,
    );
  });

  test('renders a path as a constellation of keystones and rows', async ({ page, request }) => {
    const [path] = (await runesOf(request)).trees;
    expect(path, 'the stack must carry rune paths').toBeDefined();

    await page.goto(`/en/${path?.canonicalPath}`);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText(path?.name ?? '');
    const runes = page.locator('lodb-rune-constellation article[id^="rune-"]');
    expect(await runes.count()).toBeGreaterThan(MINOR_RUNES);
    await expect(page.locator('script[type="application/ld+json"]').first()).toBeAttached();
  });
});

test.describe('rune pages', () => {
  test('lead from a rune to its card on its path page', async ({ page, consoleErrors }) => {
    await page.goto(LIST);
    await page.waitForLoadState('networkidle');
    const link = page.locator(`${CARD} a`).first();
    const anchor = (await link.getAttribute('href'))?.split('#')[1] ?? '';

    await link.click();

    await expect(page).toHaveURL(new RegExp(`/en/runes/[^#]+#${anchor}$`));
    await expect(page.locator(`#${anchor}`)).toBeInViewport();
    await page.locator('lodb-pager .pager__hub').click();
    await expect(page).toHaveURL(/\/en\/runes$/);
    expect(consoleErrors).toEqual([]);
  });
});
