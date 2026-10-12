import { expectNoAccessibilityViolations } from '../../support/accessibility';
import { pageUrl } from '../../support/public-pages';
import { expect, test } from '../../support/test';
import { useTheme, type Theme } from '../../support/theme';

// axe (WCAG 2.1 AA) on a page of each template in the default identity, and on the two richest
// in spirit-blossom, whose accents turn from gold to pale blue: contrast is checked against
// each theme's own tokens. The other pages share these templates' primitives. Contrast and
// what is shown need the real styles and layout of a browser, which jsdom has not.
const TEMPLATE_PATHS = [
  '',
  'champions',
  'champions/Annie',
  'items/3031-infinity-edge',
  'runes/8100-domination',
  'summoners/SummonerFlash',
  'account/login',
];
const RICH_PATHS = ['', 'champions/Annie'];
const THEMED_PATHS: readonly (readonly [Theme, readonly string[]])[] = [
  ['hextech', TEMPLATE_PATHS],
  ['spirit-blossom', RICH_PATHS],
];
const DEFAULT_BASE_URL = 'http://localhost:18080';

for (const [theme, paths] of THEMED_PATHS) {
  test.describe(`accessibility in the ${theme} theme`, { tag: '@readonly' }, () => {
    test.beforeEach(async ({ context, baseURL }) => {
      await useTheme(context, baseURL ?? DEFAULT_BASE_URL, theme);
    });

    for (const path of paths) {
      const url = pageUrl('en', path);

      test(`finds no violation on ${url}`, async ({ page }) => {
        await page.goto(url);
        await page.waitForLoadState('networkidle');

        await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
        await expectNoAccessibilityViolations(page);
      });
    }
  });
}

// In the default identity, which a browser without the theme's cookie gets.
test.describe('accessibility of the error and right-to-left pages', { tag: '@readonly' }, () => {
  test('finds no violation on the 404 page', async ({ page }) => {
    await page.goto('/en/champions/nowhere/at/all');
    await page.waitForLoadState('networkidle');

    await expectNoAccessibilityViolations(page);
  });

  test('finds no violation on a right-to-left page', async ({ page }) => {
    await page.goto('/ar/champions/Annie');
    await page.waitForLoadState('networkidle');

    await expectNoAccessibilityViolations(page);
  });
});
