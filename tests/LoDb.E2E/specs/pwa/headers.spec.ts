import { expect, test } from '../../support/test';

const IMMUTABLE = 'public, max-age=31536000, immutable';

// A query nobody else sends: the page cache sees a key of its own on every run.
function freshKey(path: string): string {
  return `${path}?e2e=${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

test.describe('headers of a page', { tag: '@readonly' }, () => {
  test('carry a CSP that admits exactly the inline scripts of the page', async ({ request }) => {
    const response = await request.get('/en/about');
    const policy = response.headers()['content-security-policy'] ?? '';
    const inlineScripts = (await response.text()).match(
      /<script>|<script type="text\/javascript"/g,
    );

    expect(response.status()).toBe(200);
    expect(policy).toContain("frame-ancestors 'none'");
    expect(policy).toContain("connect-src 'self'");
    // 'unsafe-eval' serves the i18n message compiler (src/server/security/document-policy.ts).
    expect(policy).toMatch(/script-src 'self'( 'unsafe-eval')?( 'sha256-[A-Za-z0-9+/]+=*')+;/);
    expect(policy).not.toMatch(/script-src[^;]*'unsafe-inline'/);
    expect(inlineScripts?.length ?? 0).toBeGreaterThan(0);
  });

  test('carry the constant headers of the edge', async ({ request }) => {
    const headers = (await request.get('/en/about')).headers();

    expect(headers['strict-transport-security']).toBe(
      'max-age=63072000; includeSubDomains; preload',
    );
    expect(headers['x-content-type-options']).toBe('nosniff');
    expect(headers['x-frame-options']).toBe('DENY');
  });

  test('come from the shared cache on the second request', async ({ request }) => {
    const path = freshKey('/en/about');

    const first = await request.get(path);
    const second = await request.get(path);

    expect(first.headers()['x-cache-status']).toBe('MISS');
    expect(second.headers()['x-cache-status']).toBe('HIT');
    expect(second.headers()['cache-control']).toContain('s-maxage=');
  });

  test('never cache the redirect of /, which depends on Accept-Language', async ({ request }) => {
    const response = await request.get('/', { maxRedirects: 0 });

    expect(response.status()).toBe(302);
    expect(response.headers()['vary']).toContain('Accept-Language');
    expect(response.headers()['x-cache-status']).toBeUndefined();
  });

  test('never cache a private page', async ({ request }) => {
    const path = freshKey('/en/account/login');

    await request.get(path);
    const second = await request.get(path);

    expect(second.headers()['cache-control']).toBe('private, no-store');
    expect(second.headers()['x-cache-status']).not.toBe('HIT');
  });
});

test.describe('headers of the front files', { tag: '@readonly' }, () => {
  test('cache the hashed bundles of /build/ for a year', async ({ request }) => {
    const html = await (await request.get('/en/about')).text();
    const bundle = /<script src="(\/build\/main-[\w-]+\.js)"/.exec(html)?.[1];

    expect(bundle).toBeDefined();
    const response = await request.get(bundle ?? '');
    expect(response.status()).toBe(200);
    expect(response.headers()['cache-control']).toBe(IMMUTABLE);
  });

  test('route no page under /build/', async ({ request }) => {
    for (const path of [
      '/build/',
      '/build/en/about',
      '/build/offline.html',
      '/build/main-00000000.js',
    ]) {
      const response = await request.get(path);

      expect(response.status(), path).toBe(404);
      expect(response.headers()['content-type'], path).toContain('text/plain');
    }
  });

  test('revalidate the worker and the manifest on every use', async ({ request }) => {
    const worker = await request.get('/sw.js');
    const manifest = await request.get('/manifest.webmanifest');

    expect(worker.headers()['content-type']).toContain('javascript');
    expect(worker.headers()['cache-control']).toBe('public, max-age=0, must-revalidate');
    expect(manifest.headers()['content-type']).toContain('application/manifest+json');
    expect((await manifest.json()) as unknown).toMatchObject({
      start_url: '/',
      display: 'standalone',
    });
  });

  test('serve the offline page with a policy that admits no script', async ({ request }) => {
    const response = await request.get('/offline.html');

    expect(response.status()).toBe(200);
    expect(response.headers()['content-security-policy']).toMatch(
      /script-src 'self'( 'unsafe-eval')?;/,
    );
  });

  test('answer a dotfile with a plain 404, without rendering', async ({ request }) => {
    const response = await request.get('/.env');

    expect(response.status()).toBe(404);
    expect(response.headers()['x-cache-status']).toBeUndefined();
  });
});
