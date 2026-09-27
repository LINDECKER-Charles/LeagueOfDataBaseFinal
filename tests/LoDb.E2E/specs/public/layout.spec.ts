import type { Page } from '@playwright/test';
import { metaOf, olderVersion } from '../../support/catalog';
import { NARROW_PHONE, overflowOf, smallFields } from '../../support/layout';
import { PUBLIC_PAGES, pageUrl } from '../../support/public-pages';
import { expect, test } from '../../support/test';

// Heritage H6: the overflow probe of the former site, on every public page, and the size of
// its fields. A page of each template in the latest version, then the archived templates,
// the 404 page and a sample of Arabic pages, laid out right to left.
const RTL_PATHS = ['', 'champions', 'champions/Annie', 'about'];
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

  for (const entry of PUBLIC_PAGES) {
    const url = pageUrl('en', entry.path);

    test(`fit ${url} in the width, fields at 16 px or more`, async ({ page }) => {
      await expectFitsPhone(page, url);
    });
  }

  test('fit the archived list and detail in the width', async ({ page, request }) => {
    const older = olderVersion(await metaOf(request));

    await expectFitsPhone(page, `/en/${older}/champions`);
    await expectFitsPhone(page, `/en/${older}/champions/Annie`);
  });

  test('fit the 404 page in the width', async ({ page }) => {
    await expectFitsPhone(page, '/en/champions/nowhere/at/all');
  });

  for (const path of RTL_PATHS) {
    const url = pageUrl('ar', path);

    test(`fit ${url}, right to left, in the width`, async ({ page }) => {
      await expectFitsPhone(page, url);
    });
  }
});

test.describe('right-to-left layout', { tag: '@readonly' }, () => {
  test.use({ viewport: DESKTOP });

  for (const path of RTL_PATHS) {
    const url = pageUrl('ar', path);

    test(`mirrors ${url}: brand on the right, the Latin name kept left to right`, async ({
      page,
    }) => {
      await page.goto(url);
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
  }
});
