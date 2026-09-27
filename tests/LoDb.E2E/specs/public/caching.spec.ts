import { metaOf, olderVersion } from '../../support/catalog';
import { expect, test } from '../../support/test';

// The Cache-Control of each class of page (core/routing/response/cache-control.ts), from the
// server through nginx: the latest version for five minutes of shared cache, an archived one
// for a week, a private page never. The shared cache itself, the front files and the service
// worker are specs/pwa's.
const LATEST = 'public, max-age=0, s-maxage=300, stale-while-revalidate=3600';
const ARCHIVED = 'public, max-age=3600, s-maxage=604800';
const PRIVATE = 'private, no-store';
const LATEST_PATHS = ['/en/', '/en/champions', '/en/items/3031-infinity-edge', '/fr/runes'];
const PRIVATE_PATHS = ['/en/account/login', '/en/account/register', '/fr/account/profile'];

test.describe('cache classes of the pages', { tag: '@readonly' }, () => {
  test('keep a page of the latest version five minutes in the shared cache', async ({
    request,
  }) => {
    for (const path of LATEST_PATHS) {
      const response = await request.get(path);

      expect(response.status(), path).toBe(200);
      expect(response.headers()['cache-control'], path).toBe(LATEST);
    }
  });

  test('keep a page of an archived version a week, which never changes', async ({ request }) => {
    const older = olderVersion(await metaOf(request));

    for (const path of [`/en/${older}/champions`, `/en/${older}/champions/Annie`]) {
      const response = await request.get(path);

      expect(response.status(), path).toBe(200);
      expect(response.headers()['cache-control'], path).toBe(ARCHIVED);
    }
  });

  test('never store a private page, nor let it be indexed', async ({ request }) => {
    for (const path of PRIVATE_PATHS) {
      const headers = (await request.get(path, { maxRedirects: 0 })).headers();

      expect(headers['cache-control'], path).toBe(PRIVATE);
      expect(headers['x-robots-tag'], path).toBe('noindex');
    }
  });

  // nginx keeps an answer that sets a cookie out of the shared cache (snippets/page-cache.conf).
  test('set no cookie on a public page, which would keep it out of the cache', async ({
    request,
  }) => {
    for (const path of [...LATEST_PATHS, '/en/about']) {
      const headers = (await request.get(path)).headers();

      expect(headers['set-cookie'], path).toBeUndefined();
    }
  });
});
