import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

// The audience measure (core/analytics): the same bundle as production, served from another
// host, never reaches Umami, neither its tracker nor its collection, whatever the visitor
// does; and the privacy policy keeps the visitor's own choice about it on this browser.
const UMAMI_PATHS = ['/u/api/send', '/u/script.js'];
const LIST = '/en/items';
const SEARCH = 'lodb-filter-console input[type=search]';
const CARD_LINK = 'lodb-catalogue-list .grid__cell a';
const OPT_OUT_KEY = 'lodb.audience.opt-out';

function umamiRequests(page: Page): string[] {
  const seen: string[] = [];
  page.on('request', (request) => {
    const { pathname } = new URL(request.url());
    if (UMAMI_PATHS.includes(pathname)) {
      seen.push(`${request.method()} ${pathname}`);
    }
  });
  return seen;
}

const pathOf = (page: Page) => new URL(page.url()).pathname;

test.describe('audience measure off the canonical host', { tag: '@readonly' }, () => {
  test('sends nothing to Umami while browsing, searching and failing', async ({
    page,
    consoleErrors,
  }) => {
    const sent = umamiRequests(page);

    // A served page, a search that rewrites the query, then a page opened by the router.
    await page.goto(LIST);
    await page.waitForLoadState('networkidle');
    await page.locator(SEARCH).fill('boots');
    await expect.poll(() => new URL(page.url()).searchParams.get('q')).toBe('boots');
    await page.locator(CARD_LINK).first().click();
    await expect.poll(() => pathOf(page)).toMatch(/^\/en\/items\/.+/);
    await page.waitForLoadState('networkidle');
    // The browser reports the 404 below as a console error of its own.
    const errorsBeforeTheFailure = [...consoleErrors];
    await page.goto('/en/items/nowhere/at/all');
    await page.waitForLoadState('networkidle');

    expect(sent).toEqual([]);
    expect(errorsBeforeTheFailure).toEqual([]);
  });

  test('keeps the opt-out of the privacy policy on this browser', async ({ page }) => {
    await page.goto('/en/legal/privacy');
    await page.waitForLoadState('networkidle');
    const toggle = page.locator('section#audience input[type=checkbox]');

    await expect(toggle).toBeChecked();
    await toggle.uncheck();
    expect(await page.evaluate((key) => localStorage.getItem(key), OPT_OUT_KEY)).toBe('1');
    await page.reload();

    await expect(page.locator('section#audience input[type=checkbox]')).not.toBeChecked();
  });
});
