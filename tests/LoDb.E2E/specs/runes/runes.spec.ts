import type { APIRequestContext } from '@playwright/test';
import { type DetailNeighbours, expectServedDetail, metaOf } from '../../support/catalog';
import { expect, test } from '../../support/test';

interface Meta {
  readonly latest: string | null;
}

interface RuneList {
  readonly entries: readonly { readonly key: string; readonly canonicalPath: string }[];
  readonly trees: readonly { readonly name: string; readonly canonicalPath: string }[];
}

interface RunePath {
  readonly neighbours: DetailNeighbours;
}

const LIST = '/en/runes';
const CARD = 'lodb-catalogue-list lodb-rune-card';
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

// A path page's payload, as /en/ shows it: the latest version, in en_US.
async function pathOf(request: APIRequestContext, canonicalPath: string): Promise<RunePath> {
  const { latest } = await metaOf(request);
  const response = await request.get(`/api/catalog/${latest}/en_US/${canonicalPath}`);
  expect(response.status()).toBe(200);
  return (await response.json()) as RunePath;
}

test.describe('rune pages as crawlers read them', { tag: '@readonly' }, () => {
  test.use({ javaScriptEnabled: false });

  test('renders a path as a constellation of keystones and rows', async ({ page, request }) => {
    const [path] = (await runesOf(request)).trees;
    expect(path, 'the stack must carry rune paths').toBeDefined();

    const response = await page.goto(`/en/${path?.canonicalPath}`);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText(path?.name ?? '');
    const { neighbours } = await pathOf(request, path?.canonicalPath ?? '');
    await expectServedDetail(page, response, neighbours);
    const runes = page.locator('lodb-rune-constellation article[id^="rune-"]');
    expect(await runes.count()).toBeGreaterThan(MINOR_RUNES);
    await expect(page.locator('script[type="application/ld+json"]').first()).toBeAttached();
  });
});

test.describe('rune pages', { tag: '@readonly' }, () => {
  test('lead from a rune to its card on its path page', async ({ page, consoleErrors }) => {
    await page.goto(LIST);
    await page.waitForLoadState('networkidle');
    const link = page.locator(`${CARD} a`).first();
    const anchor = (await link.getAttribute('href'))?.split('#')[1] ?? '';

    await link.click();

    await expect(page).toHaveURL(new RegExp(`/en/runes/[^#]+#${anchor}$`));
    const card = page.locator(`#${anchor}`);
    await expect(card).toBeInViewport();
    await page.locator('lodb-pager .pager__hub').click();
    await expect(page).toHaveURL(/\/en\/runes$/);
    expect(consoleErrors).toEqual([]);
  });
});
