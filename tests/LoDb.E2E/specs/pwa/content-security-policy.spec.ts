import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

// The main kinds of page: home, list, detail in two locales, missing page.
const PAGES = [
  '/fr/',
  '/fr/items',
  '/fr/champions/Annie',
  '/en/items/3031-infinity-edge',
  '/fr/champions/nowhere/at/all',
];

// Every violation the browser reports for the page, recorded from its very first script.
async function recordViolations(page: Page): Promise<() => Promise<string[]>> {
  await page.addInitScript(() => {
    const seen: string[] = [];
    Reflect.set(window, '__lodbViolations', seen);
    document.addEventListener('securitypolicyviolation', (event) => {
      seen.push(`${event.effectiveDirective} ${event.blockedURI} ${event.sourceFile}`);
    });
  });
  return () => page.evaluate(() => Reflect.get(window, '__lodbViolations') as string[]);
}

test.describe('the content security policy of the pages', { tag: '@readonly' }, () => {
  for (const path of PAGES) {
    test(`lets ${path} hydrate without a violation, and without eval`, async ({
      page,
      consoleErrors,
    }) => {
      const violations = await recordViolations(page);

      const response = await page.goto(path);
      await page.waitForLoadState('networkidle');

      expect(response?.headers()['content-security-policy']).not.toContain('unsafe-eval');
      expect(await violations()).toEqual([]);
      expect(consoleErrors.filter((error) => /Content Security Policy/i.test(error))).toEqual([]);
    });
  }

  // A count is an ICU plural, compiled at build time: the browser renders it without eval.
  test('renders the plural of a list count once hydrated', async ({ page }) => {
    const violations = await recordViolations(page);

    await page.goto('/fr/items');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('.toolbar__count').first()).toContainText(/résultats?/);
    expect(await violations()).toEqual([]);
  });
});
