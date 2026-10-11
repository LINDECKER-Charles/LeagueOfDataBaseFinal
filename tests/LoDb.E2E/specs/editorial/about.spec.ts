import { expect, test } from '../../support/test';

const NUMBER = /^\s*\d[\d\s., ]*\s*$/;
// Set on the document: still there later, the page changed through the router, not a load.
const MARK = 'lodbSameDocument';

test.describe('about pages', { tag: '@readonly' }, () => {
  test('fill the inventory counters in the browser', async ({ page, consoleErrors }) => {
    await page.goto('/en/about');

    await expect(page.locator('lodb-inventory-counters dd').first()).toHaveText(NUMBER);

    await test.step('goes from a prerendered page to another through the router', async () => {
      await page.waitForLoadState('networkidle');
      await page.evaluate((mark) => Reflect.set(window, mark, true), MARK);

      await page.locator('footer a[href="/en/faq"]').click();

      await expect.poll(() => new URL(page.url()).pathname).toBe('/en/faq');
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      expect(await page.evaluate((mark) => Reflect.get(window, mark) === true, MARK)).toBe(true);
    });
    expect(consoleErrors).toEqual([]);
  });
});
