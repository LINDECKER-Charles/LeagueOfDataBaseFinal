import type { Page } from '@playwright/test';
import { NARROW_PHONE, overflowOf, smallFields } from '../../support/layout';
import { pageUrl } from '../../support/public-pages';
import { expect, test } from '../../support/test';

// Heritage H6: the overflow probe of the former site, and the size of its fields, on a sample:
// the home, a list, a detail and the form with the most fields, the locales whose words run
// the longest, and an Arabic page, laid out right to left.
const SAMPLE_PATHS = ['', 'champions', 'champions/Annie', 'items', 'account/register'];
const RTL_URL = pageUrl('ar', 'champions/Annie');
const DESKTOP = { width: 1280, height: 800 };

async function expectFitsPhone(page: Page, url: string): Promise<void> {
  await page.goto(url);
  await page.waitForLoadState('networkidle');

  const overflow = await overflowOf(page, NARROW_PHONE.width);
  expect
    .soft(overflow.width, `${url} lays out wider than the phone: ${overflow.offenders.join(', ')}`)
    .toBeLessThanOrEqual(NARROW_PHONE.width);
  expect.soft(await smallFields(page), `${url}: fields under 16 px`).toEqual([]);
}

test.describe('public pages on a 320 px phone', { tag: '@readonly' }, () => {
  test.use({ viewport: NARROW_PHONE, isMobile: true, hasTouch: true, deviceScaleFactor: 2 });

  for (const path of SAMPLE_PATHS) {
    const url = pageUrl('en', path);

    test(`fit ${url} in the width, fields at 16 px or more`, async ({ page }) => {
      await expectFitsPhone(page, url);
    });
  }

  // The longest list title of the 21 locales: one German word wider than the phone.
  test('fit a list titled with one long word in the width', async ({ page }) => {
    await expectFitsPhone(page, pageUrl('de', 'summoners'));
  });

  // Rune names keep whole words: a Polish keystone name wider than its card, and the page.
  test('fit a rune path with a keystone name wider than its card', async ({ page }) => {
    await expectFitsPhone(page, pageUrl('pl', 'runes/8300-inspiration'));
  });

  test(`fit ${RTL_URL}, right to left, in the width`, async ({ page }) => {
    await expectFitsPhone(page, RTL_URL);
  });
});

test.describe('right-to-left layout', { tag: '@readonly' }, () => {
  test.use({ viewport: DESKTOP });

  test(`mirrors ${RTL_URL}: brand on the right, the Latin name kept left to right`, async ({
    page,
  }) => {
    await page.goto(RTL_URL);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
    const brand = await page.locator('header a.brand-link').boundingBox();
    const picker = await page.locator('header lodb-theme-picker').boundingBox();
    expect(brand?.x ?? 0).toBeGreaterThan(picker?.x ?? Number.POSITIVE_INFINITY);
    await expect(page.locator('header .brand-title')).toHaveAttribute('dir', 'ltr');
    expect(await page.locator('main').evaluate((main) => getComputedStyle(main).direction)).toBe(
      'rtl',
    );
  });
});
