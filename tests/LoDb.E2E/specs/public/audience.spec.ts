import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

// The audience measure (core/analytics): the same bundle as production, served from another
// host, never reaches Umami, neither its tracker nor its collection, whatever the visitor
// does; and the privacy policy keeps the visitor's own choice about it on this browser.
const UMAMI_PATHS = ['/u/api/send', '/u/script.js'];
const SEARCH = 'lodb-filter-console input[type=search]';
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

test.describe('audience measure off the canonical host', { tag: '@readonly' }, () => {
  test('sends nothing to Umami while browsing, searching and failing', async ({
    page,
    consoleErrors,
  }) => {
    const sent = umamiRequests(page);

    await page.goto('/en/');
    await page.waitForLoadState('networkidle');
    await page.locator('a[href="/en/champions"]').first().click();
    await expect.poll(() => new URL(page.url()).pathname).toBe('/en/champions');
    await page.locator(SEARCH).fill('Annie');
    await expect.poll(() => new URL(page.url()).searchParams.get('q')).toBe('Annie');
    await page.goto('/en/champions/Annie');
    await page.goto('/en/champions/nowhere/at/all');
    await page.waitForLoadState('networkidle');

    expect(sent).toEqual([]);
    expect(consoleErrors).toEqual([]);
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
