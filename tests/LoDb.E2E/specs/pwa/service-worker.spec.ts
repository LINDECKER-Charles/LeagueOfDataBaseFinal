import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

// Prerendered: available without catalogue data, so the suite runs on any stack.
const VISITED = '/en/about';
const NEVER_VISITED = '/en/faq';

/** Waits until the worker is active and controls the page, which its `claim` guarantees. */
async function controlledByWorker(page: Page): Promise<void> {
  await page.evaluate(async () => {
    await navigator.serviceWorker.ready;
    if (navigator.serviceWorker.controller === null) {
      await new Promise((resolve) =>
        navigator.serviceWorker.addEventListener('controllerchange', resolve, { once: true }),
      );
    }
  });
}

async function cachedPaths(page: Page, cacheName: string): Promise<string[]> {
  return page.evaluate(async (name) => {
    const cache = await caches.open(name);
    return (await cache.keys()).map((request) => new URL(request.url).pathname);
  }, cacheName);
}

test.describe('service worker', { tag: '@readonly' }, () => {
  test('makes the site installable and controls its pages', async ({ page }) => {
    await page.goto(VISITED);
    await controlledByWorker(page);

    await expect(page.locator('link[rel="manifest"]')).toHaveAttribute(
      'href',
      '/manifest.webmanifest',
    );
    const scriptUrl = await page.evaluate(
      () => navigator.serviceWorker.controller?.scriptURL ?? '',
    );
    expect(new URL(scriptUrl).pathname).toBe('/sw.js');
  });

  test('answers a visited page offline, and the offline page for another', async ({
    page,
    context,
  }) => {
    await page.goto(VISITED);
    await controlledByWorker(page);
    // This load goes through the worker, which keeps a copy of the page.
    await page.reload();
    await expect.poll(() => cachedPaths(page, 'lodb-pages-v1')).toContain(VISITED);
    const title = await page.title();

    await context.setOffline(true);
    await page.reload();
    await expect(page).toHaveTitle(title);

    await page.goto(NEVER_VISITED);
    await expect(page.locator('h1')).toHaveText('Connexion à la Faille perdue');
    await expect(page.locator('p[lang="en"]')).toHaveText(
      'You are offline — previously visited pages remain available.',
    );
    await context.setOffline(false);
  });

  test('keeps hashed bundles, and no private page', async ({ page }) => {
    await page.goto(VISITED);
    await controlledByWorker(page);
    await page.reload();
    await page.goto('/en/account/login');

    await expect
      .poll(() => cachedPaths(page, 'lodb-assets-v1'))
      .toContainEqual(expect.stringMatching(/^\/build\/main-[\w-]+\.js$/));
    expect(await cachedPaths(page, 'lodb-pages-v1')).not.toContainEqual(
      expect.stringContaining('/account/'),
    );
  });

  test('runs pages without any CSP violation', async ({ page, consoleErrors }) => {
    await page.goto(VISITED);
    await controlledByWorker(page);
    await page.reload();
    await page.waitForLoadState('networkidle');

    expect(consoleErrors.filter((error) => /Content Security Policy/i.test(error))).toEqual([]);
  });
});
