import { expect, test } from '../../support/test';

// `/` has no page: the SSR server sends the browser to the locale it prefers (ADR 0005).
test.describe('entry, in a French browser', () => {
  test.use({ locale: 'fr-FR' });

  test('lands on the French home, rendered by the server, then runs it', async ({
    page,
    consoleErrors,
  }) => {
    const response = await page.goto('/');

    expect(new URL(page.url()).pathname).toBe('/fr/');
    expect(response?.status()).toBe(200);
    await expect(page.locator('html')).toHaveAttribute('lang', 'fr');
    await page.waitForLoadState('networkidle');
    expect(consoleErrors).toEqual([]);
  });
});

test.describe('entry, in an Arabic browser', () => {
  test.use({ locale: 'ar-SA' });

  test('lands on the Arabic home, laid out right to left', async ({ page }) => {
    await page.goto('/');

    expect(new URL(page.url()).pathname).toBe('/ar/');
    await expect(page.locator('html')).toHaveAttribute('lang', 'ar');
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
  });
});

test.describe('entry, in a browser whose languages the site lacks', () => {
  test.use({ locale: 'nl-NL' });

  test('falls back to English', async ({ page }) => {
    await page.goto('/');

    expect(new URL(page.url()).pathname).toBe('/en/');
  });
});

test('answers / with a 302 that shared caches keep apart by language', async ({ request }) => {
  const response = await request.get('/', {
    headers: { 'Accept-Language': 'de-DE,de;q=0.9' },
    maxRedirects: 0,
  });

  expect(response.status()).toBe(302);
  expect(response.headers()['location']).toBe('/de/');
  expect(response.headers()['vary']).toContain('Accept-Language');
});
