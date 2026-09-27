import type { APIRequestContext } from '@playwright/test';
import { expect, test } from '../../support/test';

interface ErrorCopy {
  readonly error: { readonly '404': { readonly title: string } };
}

// The 404 heading of a locale, from the catalogue the site serves: the spec follows the copy.
async function notFoundTitle(request: APIRequestContext, locale: string): Promise<string> {
  const copy = (await (await request.get(`/i18n/seo/${locale}.json`)).json()) as ErrorCopy;
  return copy.error['404'].title;
}

// Chromium logs the 404 of the document itself; anything else is a real error.
function unexpected(errors: readonly string[]): string[] {
  return errors.filter((error) => !error.startsWith('Failed to load resource'));
}

test.describe('missing pages', { tag: '@readonly' }, () => {
  test('answers a real 404 in the locale of the URL, then leads back to its home', async ({
    page,
    request,
    consoleErrors,
  }) => {
    const response = await page.goto('/fr/champions/nowhere/at/all');

    expect(response?.status()).toBe(404);
    expect(response?.headers()['x-robots-tag']).toBe('noindex');
    await expect(page.locator('html')).toHaveAttribute('lang', 'fr');
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(
      await notFoundTitle(request, 'fr'),
    );

    await page.waitForLoadState('networkidle');
    await page.getByRole('main').getByRole('link').click();

    await expect.poll(() => new URL(page.url()).pathname).toBe('/fr/');
    expect(unexpected(consoleErrors)).toEqual([]);
  });

  test('answers a URL outside the 21 locales in English', async ({ page, request }) => {
    const response = await page.goto('/xx/champions');

    expect(response?.status()).toBe(404);
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(
      await notFoundTitle(request, 'en'),
    );
  });

  test('answers a pinned version alone with a 404, never a blank page', async ({ page }) => {
    const response = await page.goto('/en/15.1.1');

    expect(response?.status()).toBe(404);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });
});
