import { expect, test } from '../../support/test';

// Files nginx must hand to the API (server.d/seo.conf): the SSR server knows none of them.
test.describe('crawler files', () => {
  test('serves robots.txt from the API, pointing to the sitemap index', async ({
    request,
    baseURL,
  }) => {
    const response = await request.get('/robots.txt');
    const robots = await response.text();
    const sitemap = /^Sitemap: (.+)$/m.exec(robots)?.[1] ?? '';

    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toBe('text/plain; charset=utf-8');
    expect(response.headers()['cache-control']).toBe('public, max-age=3600');
    expect(robots).toMatch(/^User-agent: \*\n/);
    expect(robots).toContain('Disallow: /api/\n');
    expect(robots).toContain('Disallow: /*/account/\n');
    // nginx forwards the host without its port: only the name is the stack's.
    expect(new URL(sitemap).hostname).toBe(new URL(baseURL ?? '').hostname);
    expect(new URL(sitemap).pathname).toBe('/sitemap.xml');
  });

  test('serves llms.txt from the API, linking the English pages', async ({ request }) => {
    const response = await request.get('/llms.txt');
    const llms = await response.text();

    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toBe('text/markdown; charset=utf-8');
    expect(llms).toMatch(/^# League Of Data Base\n\n> Free League of Legends encyclopedia/);
    expect(llms).toMatch(/\]\(https?:\/\/[^/]+\/en\/champions\): Every champion/);
    expect(llms).toContain('## Notes');
  });
});
