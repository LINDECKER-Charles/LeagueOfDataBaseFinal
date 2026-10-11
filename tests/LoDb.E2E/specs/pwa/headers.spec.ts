import { randomBytes } from 'node:crypto';
import { expect, test } from '../../support/test';

// The random part of a fresh key: twelve hexadecimal digits.
const FRESH_BYTES = 6;
// A list reads its search, so the pre-router keeps it: any other query would be a 301.
const LIST = '/en/champions';
// nginx keeps an answer that sets a cookie out of the shared cache (snippets/page-cache.conf).
const PUBLIC_PATHS = ['/en/', LIST, '/en/items/3031-infinity-edge', '/fr/runes', '/en/about'];

// A search nobody else sends: the page cache sees a key of its own on every run.
function freshKey(path: string): string {
  return `${path}?q=e2e${randomBytes(FRESH_BYTES).toString('hex')}`;
}

test.describe('page cache', { tag: '@readonly' }, () => {
  test('serves a page rendered by the server from the shared cache', async ({ request }) => {
    const path = freshKey(LIST);

    const first = await request.get(path);
    const second = await request.get(path);

    expect(first.status()).toBe(200);
    expect(first.headers()['x-cache-status']).toBe('MISS');
    expect(second.headers()['x-cache-status']).toBe('HIT');
    expect(second.headers()['cache-control']).toContain('s-maxage=');

    await test.step('sets no cookie on a public page, which would keep it out', async () => {
      for (const publicPath of PUBLIC_PATHS) {
        const headers = (await request.get(publicPath)).headers();

        expect(headers['set-cookie'], publicPath).toBeUndefined();
      }
    });

    await test.step('never caches the redirect of /, which varies by language', async () => {
      const response = await request.get('/', { maxRedirects: 0 });

      expect(response.status()).toBe(302);
      expect(response.headers()['vary']).toContain('Accept-Language');
      expect(response.headers()['x-cache-status']).toBeUndefined();
    });

    await test.step('never caches a private page', async () => {
      const privatePath = freshKey('/en/account/login');

      await request.get(privatePath);
      const again = await request.get(privatePath);

      expect(again.headers()['cache-control']).toBe('private, no-store');
      expect(again.headers()['x-cache-status']).not.toBe('HIT');
    });
  });
});
