import type { BrowserContext, Page } from '@playwright/test';
import { metaOf, olderVersion } from '../../support/catalog';
import { hydrated } from '../../support/hydration';
import { expect, test } from '../../support/test';

// Another patch or language is warmed behind the loader, which follows the real API ingesting
// it, then visited through the router: the ingestion and the browser's navigation only meet on
// the stack. The switcher's choices and the loader's states are Vitest's.
const PREFERENCES_COOKIE = 'lod_prefs';
// Every internal navigation sends its page-view beacon: telemetry, not a preference.
const ANALYTICS_PATH = '/api/analytics/';
// A cold patch: its datasets, then the images of a list, fetched from Data Dragon.
const WARM_UP_TIMEOUT_MS = 60_000;

function switcher(page: Page) {
  return page.locator('lodb-context-switcher');
}

function panelOf(page: Page) {
  return switcher(page).locator('details');
}

// Opens the panel once the app drives the switcher. A completed navigation folds the panel
// (lodbDisclosure): the initial one only ends before the app first settles, which hydrated()
// waits for, and the options the browser loads are there by then.
async function openSwitcher(page: Page): Promise<void> {
  await hydrated(switcher(page));
  await expect(switcher(page).locator('#switcher-version option')).not.toHaveCount(0);
  await expect(panelOf(page)).not.toHaveAttribute('open');
  await switcher(page).locator('summary').click();
  await expect(panelOf(page)).toHaveAttribute('open', '');
}

function loaderOf(page: Page) {
  return page.locator('lodb-loader-dialog');
}

// Sends the choice, then waits for the loader to come and go: another context is warmed
// behind it before the visit, for seconds when the stack has not ingested the patch yet.
async function apply(page: Page): Promise<void> {
  await switcher(page).getByRole('button').click();
  await expect(panelOf(page)).not.toHaveAttribute('open');
  await expect(loaderOf(page)).toBeVisible();
  await expect(loaderOf(page)).toBeHidden({ timeout: WARM_UP_TIMEOUT_MS });
}

async function preferencesCookie(context: BrowserContext): Promise<string | undefined> {
  const cookies = await context.cookies();
  return cookies.find((cookie) => cookie.name === PREFERENCES_COOKIE)?.value;
}

function pathAndQuery(page: Page): string {
  const url = new URL(page.url());
  return `${url.pathname}${url.search}`;
}

test.describe('context switcher', { tag: '@readonly' }, () => {
  // The legacy loader: the patch is warmed behind a modal naming the page's lists, then
  // visited. It holds its full bar a beat even on a warm patch, which the check relies on.
  test('warms the chosen patch behind the loader, then lands on the page', async ({
    page,
    request,
    context,
    consoleErrors,
  }) => {
    // Three data sets warmed in one journey (the older patch, then fr, then en_GB): each may
    // be cold on a fresh stack.
    test.slow();
    const older = olderVersion(await metaOf(request));
    const posts: string[] = [];
    page.on('request', (sent) => {
      if (sent.method() === 'POST' && !new URL(sent.url()).pathname.startsWith(ANALYTICS_PATH)) {
        posts.push(sent.url());
      }
    });
    await page.goto('/en/champions');

    await openSwitcher(page);
    await switcher(page).locator('#switcher-version').selectOption(older);
    await switcher(page).getByRole('button').click();

    const loader = page.getByRole('dialog', { name: 'Summoning data' });
    await expect(loader).toBeVisible();
    await expect(loader.getByRole('listitem')).toHaveText([/Champions/]);
    await expect(loader.getByRole('progressbar')).toBeAttached();
    await expect(loader).toBeHidden({ timeout: WARM_UP_TIMEOUT_MS });
    expect(pathAndQuery(page)).toBe(`/en/${older}/champions`);

    await test.step('pins the older patch on the same page, without a POST', async () => {
      expect(posts).toEqual([]);
      expect(await preferencesCookie(context)).toBeUndefined();
      expect(consoleErrors).toEqual([]);
    });

    // A tab of its own: the pinned patch stays in the session storage of the first one.
    await test.step('moves to another locale, then to a regional variant by ?lang=', async () => {
      const tab = await context.newPage();
      await tab.goto('/en/items');

      await openSwitcher(tab);
      await switcher(tab).locator('#switcher-language').selectOption('fr:fr_FR');
      await apply(tab);
      await expect.poll(() => pathAndQuery(tab)).toBe('/fr/items');

      await openSwitcher(tab);
      await switcher(tab).locator('#switcher-language').selectOption('en:en_GB');
      await apply(tab);
      await expect.poll(() => pathAndQuery(tab)).toBe('/en/items?lang=en_GB');
      await tab.close();
    });
  });
});
