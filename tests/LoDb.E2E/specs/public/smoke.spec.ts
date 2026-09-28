import { expect, test } from '../../support/test';

test.describe('smoke', { tag: '@readonly' }, () => {
  test('serves /en/ rendered by the SSR server, then runs it without console error', async ({
    page,
    consoleErrors,
  }) => {
    const response = await page.goto('/en/');

    expect(response?.status()).toBe(200);
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await page.waitForLoadState('networkidle');
    expect(consoleErrors).toEqual([]);
  });

  test.describe('without JavaScript', () => {
    test.use({ javaScriptEnabled: false });

    // The pages rendered in the browser need it; a server-rendered page works without. Text
    // locators skip <noscript>, hence the CSS one.
    test('says so on a page rendered in the browser only', async ({ page }) => {
      const notice = page.locator('lodb-root noscript p');

      await page.goto('/en/account/login');
      await expect(notice).toBeVisible();
      expect(await notice.evaluate((element) => element.textContent)).toBe(
        'JavaScript is required to use this page.',
      );
      // Clear of the screen's edge: index.html's classes must reach the global stylesheet.
      expect((await notice.boundingBox())?.x).toBeGreaterThan(0);
      await page.goto('/en/about');
      await expect(notice).toHaveCount(0);
    });
  });

  test('delivers the page as server-rendered HTML', async ({ request }) => {
    const response = await request.get('/en/');

    expect(response.status()).toBe(200);
    expect(await response.text()).toContain('ng-server-context="ssr"');
  });
});
