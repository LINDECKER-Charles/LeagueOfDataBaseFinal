import { expect, test } from '../../support/test';

test.describe('smoke', () => {
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

  test('delivers the page as server-rendered HTML', async ({ request }) => {
    const response = await request.get('/en/');

    expect(response.status()).toBe(200);
    expect(await response.text()).toContain('ng-server-context="ssr"');
  });
});
