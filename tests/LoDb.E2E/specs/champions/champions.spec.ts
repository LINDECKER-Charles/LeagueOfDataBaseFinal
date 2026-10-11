import type { APIRequestContext, Page } from '@playwright/test';
import { expect, test } from '../../support/test';

interface Meta {
  readonly latest: string | null;
}

interface Details {
  readonly canonicalPath: string;
  readonly profile: { readonly name: string };
  readonly alternateSkinCount: number;
}

// A champion with a full kit, clips, lore, tips and skins in every patch since 2012.
const CHAMPION = 'Annie';
const LIST = '/en/champions';
const DETAIL = `${LIST}/${CHAMPION}`;
const SERVER_TIMING = /(?:^|,\s*)catalogue;dur=\d+(?:\.\d+)?/;
const KEEP = 'lodbLeftVideo';
const STAGE = '#abilities lodb-ability-media';
const LOAD_TIME = 'lodb-load-time .perf';

async function latestOf(request: APIRequestContext): Promise<string> {
  const meta = (await (await request.get('/api/meta')).json()) as Meta;
  expect(meta.latest, 'the stack must have ingested a version').not.toBeNull();
  return meta.latest ?? '';
}

async function detailsOf(request: APIRequestContext): Promise<Details> {
  const latest = await latestOf(request);
  const response = await request.get(`/api/catalog/${latest}/en_US/champions/${CHAMPION}`);
  return (await response.json()) as Details;
}

async function openDetail(page: Page): Promise<void> {
  await page.goto(DETAIL);
  await page.waitForLoadState('networkidle');
}

// The JSON-LD types the server wrote, whatever their nesting in @graph.
async function jsonLdTypes(page: Page): Promise<string[]> {
  const scripts = await page.locator('script[type="application/ld+json"]').allTextContents();
  return scripts.flatMap((script) =>
    [...script.matchAll(/"@type":"([^"]+)"/g)].map((m) => m[1] ?? ''),
  );
}

test.describe('champion pages as crawlers read them', { tag: '@readonly' }, () => {
  test.use({ javaScriptEnabled: false });

  test('renders a champion whole, with its head and the time the entity took', async ({
    page,
    request,
  }) => {
    const details = await detailsOf(request);

    const response = await page.goto(DETAIL);

    expect(response?.status()).toBe(200);
    expect(response?.headers()['server-timing']).toMatch(SERVER_TIMING);
    // The pager comes with the page's payload: its neighbours are in the server's HTML; the
    // skins do not, only their count, the gallery reading them once scrolled to.
    const html = (await response?.text()) ?? '';
    expect(html).toMatch(/<a [^>]*rel="next"[^>]*href="\/en\/champions\/\w/);
    expect(html).not.toContain('"chromas":');
    await expect(page.locator('#skins .codex-header__meta')).toHaveText(
      String(details.alternateSkinCount),
    );
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(details.profile.name);
    for (const id of ['abilities', 'skins', 'lore', 'tips', 'stats']) {
      await expect(page.locator(`#${id}`)).toHaveCount(1);
    }
    expect(
      new URL((await page.locator('link[rel="canonical"]').getAttribute('href')) ?? '').pathname,
    ).toBe(`/en/${details.canonicalPath}`);
    expect(await jsonLdTypes(page)).toEqual(
      expect.arrayContaining(['BreadcrumbList', 'VideoGame']),
    );
    // The clip waits for the browser, the skins for the reader to scroll to them.
    await expect(page.locator('#abilities video')).toHaveCount(0);
    await expect(page.locator('#abilities img.poster')).toHaveCount(1);
    await expect(page.locator('lodb-skin-gallery')).toHaveCount(0);
  });
});

test.describe('champion page in the browser', { tag: '@readonly' }, () => {
  test('plays the clip muted, and stops it when the reader leaves', async ({
    page,
    consoleErrors,
  }) => {
    await openDetail(page);
    await expect(page.locator(LOAD_TIME)).toContainText('ms');

    await test.step('hydrates every section it scrolls through without a mismatch', async () => {
      for (const id of ['abilities', 'stats', 'lore', 'tips']) {
        await page.locator(`#${id}`).scrollIntoViewIfNeeded();
      }
      await page.locator('lodb-pager').scrollIntoViewIfNeeded();
      await page.locator('lodb-footer').scrollIntoViewIfNeeded();
      await page.locator('lodb-stat-board input[type=range]').fill('18');

      await expect(page.locator('lodb-stat-board')).toContainText('18 / 18');
      await page.waitForLoadState('networkidle');
    });

    const video = page.locator('#abilities video');
    await page.locator(STAGE).scrollIntoViewIfNeeded();
    await expect(video).toHaveCount(1);
    expect(await video.evaluate((element: HTMLVideoElement) => element.muted)).toBe(true);
    await video.evaluate((element, key) => Reflect.set(window, key, element), KEEP);

    await test.step('pages to the next champion through the router, badged too', async () => {
      await page.locator('lodb-pager a[rel="next"]').click();

      await expect.poll(() => new URL(page.url()).pathname).not.toBe(DETAIL);
      await expect(page.getByRole('heading', { level: 1 })).not.toHaveText(CHAMPION);
      await expect(page.locator(LOAD_TIME)).toContainText('ms');
    });

    const paused = await page.evaluate(
      (key) => (Reflect.get(window, key) as HTMLVideoElement).paused,
      KEEP,
    );
    expect(paused).toBe(true);
    expect(consoleErrors).toEqual([]);
  });
});
