import { isSameDocument, markDocument } from '../../support/same-document';
import { expect, test } from '../../support/test';

// A page the build prerendered, hydrated in the browser: its counters filled from the real
// API, then another prerendered page reached through the router, not reloaded. Prerendering
// and hydration only meet on the stack; the counters and the pages are Vitest's.
const NUMBER = /^\s*\d[\d\s., ]*\s*$/;

test.describe('about pages', { tag: '@readonly' }, () => {
  test('fill the inventory counters in the browser', async ({ page, consoleErrors }) => {
    await page.goto('/en/about');

    await expect(page.locator('lodb-inventory-counters dd').first()).toHaveText(NUMBER);

    await test.step('goes from a prerendered page to another through the router', async () => {
      await markDocument(page);

      await page.locator('footer a[href="/en/faq"]').click();

      await expect.poll(() => new URL(page.url()).pathname).toBe('/en/faq');
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      expect(await isSameDocument(page)).toBe(true);
    });
    expect(consoleErrors).toEqual([]);
  });
});
