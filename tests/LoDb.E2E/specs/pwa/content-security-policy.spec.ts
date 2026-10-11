import { recordViolations } from '../../support/policy-violations';
import { expect, test } from '../../support/test';

// The main kinds of page: home, list, detail in two locales, missing page, a page prerendered
// at build time, and the account's pages, rendered in the browser.
const PAGES = [
  '/fr/',
  '/fr/items',
  '/fr/champions/Annie',
  '/en/items/3031-infinity-edge',
  '/fr/champions/nowhere/at/all',
  '/en/about',
  '/en/account/login',
  '/fr/account/register',
];

test.describe('the content security policy of the pages', { tag: '@readonly' }, () => {
  for (const path of PAGES) {
    test(`lets ${path} hydrate without a violation, Trusted Types included`, async ({
      page,
      consoleErrors,
    }) => {
      const violations = await recordViolations(page);

      const response = await page.goto(path);
      await page.waitForLoadState('networkidle');

      const headers = response?.headers() ?? {};
      expect(headers['content-security-policy']).not.toContain('unsafe-eval');
      expect(headers['content-security-policy-report-only']).toContain(
        "require-trusted-types-for 'script'",
      );
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

  // The theme, set before the first paint by an inline script, still applies.
  test('runs the inline script of the theme', async ({ page }) => {
    await page.goto('/en/');

    await expect(page.locator('html')).toHaveAttribute('data-theme', 'hextech');
  });

  test('names the endpoint its violations are reported to', async ({ request }) => {
    const headers = (await request.get('/en/about')).headers();

    expect(headers['reporting-endpoints']).toBe('csp="/api/csp-reports"');
    expect(headers['content-security-policy']).toContain('report-to csp');
    expect(headers['content-security-policy-report-only']).toContain('report-to csp');
  });
});
