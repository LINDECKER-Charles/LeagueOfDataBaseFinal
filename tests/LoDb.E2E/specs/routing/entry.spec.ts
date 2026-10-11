import { expect, test } from '../../support/test';

// `/` has no page: the SSR server sends the browser to the locale it prefers (ADR 0005).
test.describe('entry, in a French browser', { tag: '@readonly' }, () => {
  test.use({ locale: 'fr-FR' });

  test('lands on the French home, rendered by the server, then runs it', async ({
    page,
    request,
    consoleErrors,
  }) => {
    const response = await page.goto('/');

    expect(new URL(page.url()).pathname).toBe('/fr/');
    expect(response?.status()).toBe(200);
    await expect(page.locator('html')).toHaveAttribute('lang', 'fr');
    await page.waitForLoadState('networkidle');
    expect(consoleErrors).toEqual([]);

    await test.step('answers / with a 302 that shared caches keep apart by language', async () => {
      const redirect = await request.get('/', {
        headers: { 'Accept-Language': 'de-DE,de;q=0.9' },
        maxRedirects: 0,
      });

      expect(redirect.status()).toBe(302);
      expect(redirect.headers()['location']).toBe('/de/');
      expect(redirect.headers()['vary']).toContain('Accept-Language');
    });
  });
});
