import { expect, test } from '../../support/test';

// What the SSR server answers from the shape of a URL alone, before any render
// (src/server/routing), through nginx: a page has one URL, and an address outside the
// grammar of ADR 0005 gets a page built with the server, not a render.
const MOVED_PERMANENTLY = 301;
const NOT_FOUND = 404;
const NEVER_STORED = 'no-store';
const STATIC_NOT_FOUND_CACHE = 'public, max-age=0, s-maxage=3600';

test.describe('URLs settled before any render', { tag: '@readonly' }, () => {
  test('a parameter the page does not read redirects to its URL, out of every cache', async ({
    request,
  }) => {
    const rows = [
      { url: '/en/champions?utm_source=mail&q=ahri', location: '/en/champions?q=ahri' },
      { url: '/en/items/1036?x=1', location: '/en/items/1036' },
      { url: '/fr/runes?path=8100&path=8000', location: '/fr/runes?path=8100' },
    ];
    for (const { url, location } of rows) {
      const response = await request.get(url, { maxRedirects: 0 });

      expect(response.status(), url).toBe(MOVED_PERMANENTLY);
      expect(response.headers()['location'], url).toBe(location);
      expect(response.headers()['cache-control'], url).toBe(NEVER_STORED);
    }
  });

  test('the parameters a list reads stay, without a redirect', async ({ request }) => {
    const url = '/en/items?tag=Boots&page=2&lang=en_GB';

    const response = await request.get(url, { maxRedirects: 0 });

    expect(response.status()).toBe(200);
  });

  test('an address outside the grammar gets the static 404, without a script', async ({
    request,
  }) => {
    const response = await request.get('/wordpress/wp-admin/', { maxRedirects: 0 });
    const headers = response.headers();
    const body = await response.text();

    expect(response.status()).toBe(NOT_FOUND);
    expect(headers['content-type']).toContain('text/html');
    expect(headers['cache-control']).toBe(STATIC_NOT_FOUND_CACHE);
    expect(headers['x-robots-tag']).toContain('noindex');
    expect(headers['content-security-policy']).not.toContain('sha256-');
    expect(body).toContain('404');
    expect(body).not.toContain('<script');
  });
});
