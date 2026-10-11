import { expect, test } from '../../support/test';

// One of the pages and locales the build prerenders (app.routes.server.ts lists them all): a
// right to left locale, under a nested path.
const LOCALE = 'ar';
const PATH = 'legal/cookies';

// What the server answers before any script runs: the file the build prerendered.
test.describe('prerendered editorial pages', { tag: '@readonly' }, () => {
  test.use({ javaScriptEnabled: false });

  test(`serves /${LOCALE}/${PATH} as a static page`, async ({ page }) => {
    const response = await page.goto(`/${LOCALE}/${PATH}`);

    expect(response?.status()).toBe(200);
    await expect(page.locator('lodb-root')).toHaveAttribute('ng-server-context', 'ssg');
    await expect(page.locator('html')).toHaveAttribute('lang', LOCALE);
    await expect(page.locator('h1')).toHaveCount(1);
    await expect(page.locator('h1')).not.toHaveText(/^\s*[a-z_]+(\.[a-z_]+)+\s*$/);
  });
});
