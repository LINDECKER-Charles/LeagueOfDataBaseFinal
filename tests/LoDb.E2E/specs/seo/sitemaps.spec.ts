import { expect, test } from '../../support/test';

const XML_TYPE = 'application/xml; charset=utf-8';
const LOCALE_COUNT = 21;
const SITEMAP_FILE = /\/sitemaps\/[a-z-]+\/(?:latest|\d+(?:\.\d+)+)\.xml$/;

function locations(xml: string): string[] {
  return [...xml.matchAll(/<loc>([^<]+)<\/loc>/g)].map((match) => match[1] ?? '');
}

test.describe('sitemaps', { tag: '@readonly' }, () => {
  test('indexes every locale sitemap of every version', async ({ request }) => {
    const response = await request.get('/sitemap.xml');
    // Without Data Dragon's list of versions the index is unavailable, never wrong.
    test.skip(response.status() === 503, 'Data Dragon unreachable from the stack');
    const sitemaps = locations(await response.text());

    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toBe(XML_TYPE);
    expect(sitemaps[0]).toMatch(/\/sitemaps\/ar\/latest\.xml$/);
    expect(sitemaps.length % LOCALE_COUNT).toBe(0);
    expect(sitemaps.filter((url) => !SITEMAP_FILE.test(url))).toEqual([]);
  });

  test("lists a locale's static pages under their short URLs", async ({ request }) => {
    const response = await request.get('/sitemaps/fr/latest.xml');
    const body = await response.text();
    const pages = locations(body).map((url) => new URL(url).pathname);

    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toBe(XML_TYPE);
    expect(body).toContain('xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"');
    expect(body).not.toContain('<lastmod>');
    expect(pages.slice(0, 5)).toEqual([
      '/fr/',
      '/fr/champions',
      '/fr/items',
      '/fr/runes',
      '/fr/summoners',
    ]);
    expect(pages).toContain('/fr/legal/privacy');
  });

  test("moves the previous site's index to the new one", async ({ request }) => {
    const response = await request.get('/sitemaps/latest.xml', { maxRedirects: 0 });

    expect(response.status()).toBe(301);
    expect(response.headers()['location']).toBe('/sitemap.xml');
    expect(response.headers()['cache-control']).toBe('public, max-age=0, s-maxage=60');
  });

  test("moves the previous site's version sitemap to its English one", async ({ request }) => {
    const response = await request.get('/sitemaps/9.1.1.xml', { maxRedirects: 0 });

    expect(response.status()).toBe(301);
    expect(response.headers()['location']).toMatch(/^\/sitemaps\/en\/(?:9\.1\.1|latest)\.xml$/);
  });

  test('answers 404 for an unknown locale or file', async ({ request }) => {
    expect((await request.get('/sitemaps/xx/latest.xml')).status()).toBe(404);
    expect((await request.get('/sitemaps/en/latest.txt')).status()).toBe(404);
  });
});
