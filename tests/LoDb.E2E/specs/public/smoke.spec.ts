import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

// The home as the built images serve it: rendered by the SSR server from the real API, then
// hydrated without an error. The one check that nginx, the SSR server, the API and the bundles
// work together; the home's content is Vitest's.
const RESOURCES = ['champions', 'items', 'runes', 'summoners'];

// The four portals of the hero, in the order of the header's codex.
function portals(page: Page) {
  return page.getByRole('main').locator('section').first().getByRole('listitem').getByRole('link');
}

test.describe('smoke', { tag: '@readonly' }, () => {
  test('serves /en/ rendered by the SSR server, then runs it without console error', async ({
    page,
    request,
    consoleErrors,
  }) => {
    const served = await request.get('/en/');
    const html = await served.text();
    expect(served.status()).toBe(200);

    await test.step('delivers the page as server-rendered HTML', async () => {
      expect(html).toContain('ng-server-context="ssr"');
    });

    await test.step('renders its hero, portals and previews on the server', async () => {
      for (const resource of RESOURCES) {
        expect(html).toContain(`href="/en/${resource}"`);
      }
      expect(html).toContain('href="/en/champions/');
    });

    const response = await page.goto('/en/');

    expect(response?.status()).toBe(200);
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');

    await test.step('opens four portals and previews each resource', async () => {
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      await expect(portals(page)).toHaveCount(RESOURCES.length);
      // The footer has headings of its own: only the page's are counted.
      await expect(page.getByRole('main').getByRole('heading', { level: 2 })).toHaveCount(
        RESOURCES.length,
      );
    });

    await page.waitForLoadState('networkidle');

    await test.step('replaces the server head instead of adding to it', async () => {
      await expect(page.locator('link[rel="canonical"]')).toHaveCount(1);
      await expect(page.locator('meta[name="robots"]')).toHaveCount(1);
    });
    expect(consoleErrors).toEqual([]);
  });
});
