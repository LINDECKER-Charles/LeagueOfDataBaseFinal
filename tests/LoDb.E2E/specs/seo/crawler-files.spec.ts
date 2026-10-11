import { expect, test } from '../../support/test';

const OK = 200;
// Without Data Dragon's list of versions the sitemap index is unavailable, never wrong.
const UNAVAILABLE = 503;

// Files nginx must hand to the API (server.d/seo.conf): the SSR server knows none of them.
// Their content is the API tests'; here, that each one reaches the API through nginx.
test.describe('crawler files', { tag: '@readonly' }, () => {
  test('serves robots.txt, llms.txt and the sitemap index from the API', async ({ request }) => {
    const robots = await request.get('/robots.txt');

    expect(robots.status()).toBe(OK);
    expect(robots.headers()['content-type']).toBe('text/plain; charset=utf-8');

    await test.step('serves llms.txt from the API', async () => {
      const llms = await request.get('/llms.txt');

      expect(llms.status()).toBe(OK);
      expect(llms.headers()['content-type']).toBe('text/markdown; charset=utf-8');
    });

    await test.step('serves the sitemap index from the API', async () => {
      const index = await request.get('/sitemap.xml');
      test.skip(index.status() === UNAVAILABLE, 'Data Dragon unreachable from the stack');

      expect(index.status()).toBe(OK);
      expect(index.headers()['content-type']).toBe('application/xml; charset=utf-8');
    });
  });
});
