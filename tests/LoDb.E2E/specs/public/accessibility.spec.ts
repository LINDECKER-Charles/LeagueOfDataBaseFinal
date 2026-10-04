import { expectNoAccessibilityViolations } from '../../support/accessibility';
import { pageUrl } from '../../support/public-pages';
import { expect, test } from '../../support/test';
import { useTheme, type Theme } from '../../support/theme';

// axe (WCAG 2.1 AA) on the key pages, in the default identity and in spirit-blossom, whose
// accents turn from gold to pale blue: contrast is checked against each theme's own tokens.
const KEY_PATHS = [
  '',
  'champions',
  'champions/Annie',
  'items',
  'items/3031-infinity-edge',
  'runes/8100-domination',
  'summoners/SummonerFlash',
  'about',
  'faq',
  'legal/privacy',
  'account/login',
];
const CHECKED_THEMES: readonly Theme[] = ['hextech', 'spirit-blossom'];
const DEFAULT_BASE_URL = 'http://localhost:18080';

for (const theme of CHECKED_THEMES) {
  test.describe(`accessibility in the ${theme} theme`, { tag: '@readonly' }, () => {
    test.beforeEach(async ({ context, baseURL }) => {
      await useTheme(context, baseURL ?? DEFAULT_BASE_URL, theme);
    });

    for (const path of KEY_PATHS) {
      const url = pageUrl('en', path);

      test(`finds no violation on ${url}`, async ({ page }) => {
        await page.goto(url);
        await page.waitForLoadState('networkidle');

        await expect(page.locator('html')).toHaveAttribute('data-theme', theme);
        await expectNoAccessibilityViolations(page);
      });
    }

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
}
