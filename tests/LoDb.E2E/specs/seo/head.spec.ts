import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

const LOCALE_COUNT = 21;
const SITE_NAME = 'League Of Data Base';

// Prerendered pages carry production's origin, rendered ones the stack's: paths only compare.
async function canonicalPath(page: Page): Promise<string> {
  const href = await page.locator('link[rel="canonical"]').getAttribute('href');
  return new URL(href ?? '').pathname;
}

async function hreflangs(page: Page): Promise<string[]> {
  const links = await page.locator('link[rel="alternate"][hreflang]').all();
  return Promise.all(links.map(async (link) => (await link.getAttribute('hreflang')) ?? ''));
}

// The head as the server wrote it, which is what crawlers read.
test.describe('head of the server render', () => {
  test.use({ javaScriptEnabled: false });

  test('gives the home one canonical, 21 alternates and x-default', async ({ page }) => {
    await page.goto('/en/');
    const languages = await hreflangs(page);

    await expect(page.locator('link[rel="canonical"]')).toHaveCount(1);
    expect(await canonicalPath(page)).toBe('/en/');
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'index, follow');
    expect(languages.filter((language) => language !== 'x-default')).toHaveLength(LOCALE_COUNT);
    expect(new Set(languages).size).toBe(LOCALE_COUNT + 1);
    expect(languages).toContain('x-default');
  });

  test('keeps an error page out of the index, without canonical', async ({ page }) => {
    const response = await page.goto('/en/champions/nowhere/at/all');

    expect(response?.status()).toBe(404);
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
    await expect(page.locator('link[rel="canonical"]')).toHaveCount(0);
    await expect(page.locator('link[rel="alternate"][hreflang]')).toHaveCount(0);
  });

  test('gives a prerendered editorial page its canonical and its own title', async ({ page }) => {
    await page.goto('/en/about');

    await expect(page.locator('link[rel="canonical"]')).toHaveCount(1);
    expect(await canonicalPath(page)).toBe('/en/about');
    await expect(page).toHaveTitle(new RegExp(` — ${SITE_NAME}$`));
  });
});

test.describe('head after hydration', () => {
  test('replaces the server head instead of adding to it', async ({ page, consoleErrors }) => {
    await page.goto('/en/');
    await page.waitForLoadState('networkidle');

    await expect(page.locator('link[rel="canonical"]')).toHaveCount(1);
    await expect(page.locator('meta[name="robots"]')).toHaveCount(1);
    expect(consoleErrors).toEqual([]);
  });
});
