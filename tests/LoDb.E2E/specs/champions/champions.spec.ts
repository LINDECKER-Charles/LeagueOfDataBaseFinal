import type { APIRequestContext, Page } from '@playwright/test';
import { expectNoAccessibilityViolations } from '../../support/accessibility';
import { expect, test } from '../../support/test';

interface Meta {
  readonly latest: string | null;
}

interface Details {
  readonly canonicalPath: string;
  readonly profile: { readonly name: string };
  readonly skins: readonly { readonly number: number; readonly name: string }[];
}

// A champion with a full kit, clips, lore, tips and skins in every patch since 2012.
const CHAMPION = 'Annie';
const LIST = '/en/champions';
const DETAIL = `${LIST}/${CHAMPION}`;
const SERVER_TIMING = /(?:^|,\s*)catalogue;dur=\d+(?:\.\d+)?/;
const KEEP = 'lodbLeftVideo';
// The chips of the legacy tab bar, never a catalogue key.
const SECTION_LABELS = ['Abilities', 'Skins', 'Lore', 'Tips', 'Base Statistics'];

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

  test('renders the list with its version once, and links every champion', async ({
    page,
    request,
  }) => {
    const latest = await latestOf(request);

    await page.goto(LIST);

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await expect(page.getByRole('main').getByText(latest)).toHaveCount(1);
    await expect(page.locator(`lodb-champion-card a[href="${DETAIL}"]`)).toHaveCount(1);
  });

  test('renders a champion whole, with its head and the time the entity took', async ({
    page,
    request,
  }) => {
    const details = await detailsOf(request);

    const response = await page.goto(DETAIL);

    expect(response?.status()).toBe(200);
    expect(response?.headers()['server-timing']).toMatch(SERVER_TIMING);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(details.profile.name);
    for (const id of ['abilities', 'skins', 'lore', 'tips', 'stats']) {
      await expect(page.locator(`#${id}`)).toHaveCount(1);
    }
    await expect(page.locator('.section-nav a')).toHaveText(SECTION_LABELS);
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

  test('links back to the list in the regional variant it was read in', async ({
    page,
    request,
  }) => {
    await page.goto(`${DETAIL}?lang=en_GB`);

    const hub = page.locator('lodb-pager a.pager__hub');
    await expect(hub).toHaveAttribute('href', `${LIST}?lang=en_GB`);
    const response = await request.get((await hub.getAttribute('href')) ?? '');
    expect(response.status()).toBe(200);
  });
});

test.describe('champion page in the browser', { tag: '@readonly' }, () => {
  test('plays the clip muted, and stops it when the reader leaves', async ({
    page,
    consoleErrors,
  }) => {
    await openDetail(page);
    const video = page.locator('#abilities video');

    await expect(video).toHaveCount(1);
    expect(await video.evaluate((element: HTMLVideoElement) => element.muted)).toBe(true);
    await video.evaluate((element, key) => Reflect.set(window, key, element), KEEP);

    await page.locator('lodb-pager .pager__hub').click();

    await expect.poll(() => new URL(page.url()).pathname).toBe(LIST);
    const paused = await page.evaluate(
      (key) => (Reflect.get(window, key) as HTMLVideoElement).paused,
      KEEP,
    );
    expect(paused).toBe(true);
    expect(consoleErrors).toEqual([]);
  });

  test('switches ability with the keyboard, the clip following', async ({ page }) => {
    await openDetail(page);
    const tabs = page.getByRole('tab');

    await tabs.first().focus();
    await page.keyboard.press('ArrowRight');

    await expect(tabs.nth(1)).toHaveAttribute('aria-selected', 'true');
    await expect(tabs.nth(1)).toBeFocused();
    await expect(page.locator('#ability-panel-q')).toBeVisible();
  });

  test('loads the skins once scrolled to, and shows one whole in the viewer', async ({
    page,
    request,
  }) => {
    const details = await detailsOf(request);
    const skins = details.skins.filter((skin) => skin.number !== 0);
    await openDetail(page);

    await page.locator('#skins').scrollIntoViewIfNeeded();
    const tile = page.locator('#skins button.tile').first();
    await expect(tile).toBeVisible();
    await tile.click();

    const viewer = page.getByRole('dialog');
    await expect(viewer.getByRole('heading')).toHaveText(details.profile.name);
    await expect(viewer.locator('figcaption p')).toHaveText(skins[0]?.name ?? '');
    await page.keyboard.press('ArrowRight');
    await expect(viewer.locator('figcaption p')).toHaveText(skins[1]?.name ?? '');
    await page.keyboard.press('Escape');
    await expect(viewer).toHaveCount(0);
    await expect(tile).toBeFocused();
  });

  test('pages to the next champion through the router, and badges the load time', async ({
    page,
    consoleErrors,
  }) => {
    await openDetail(page);
    await expect(page.locator('lodb-load-time .perf')).toContainText('ms');

    await page.locator('lodb-pager a[rel="next"]').click();

    await expect.poll(() => new URL(page.url()).pathname).not.toBe(DETAIL);
    await expect(page.getByRole('heading', { level: 1 })).not.toHaveText(CHAMPION);
    await expect(page.locator('lodb-load-time .perf')).toContainText('ms');
    expect(consoleErrors).toEqual([]);
  });

  test('meets WCAG 2.1 AA, on the list and on a champion', async ({ page }) => {
    await page.goto(LIST);
    await page.waitForLoadState('networkidle');
    await expectNoAccessibilityViolations(page);

    await openDetail(page);
    await expectNoAccessibilityViolations(page);
  });
});
