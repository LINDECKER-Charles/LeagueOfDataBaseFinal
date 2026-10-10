// Boots the built nginx image behind stub upstreams and checks its bounds: rates per client,
// body sizes, the paths answered by nginx alone, the blobs and the caches. Needs Docker and
// the image's tag in LODB_NGINX_IMAGE (tools/nginx/README.md); skipped without it.
import assert from 'node:assert/strict';
import { after, before, describe, test } from 'node:test';
import { send, statusesOf } from '../lib/client.mjs';
import { startStack } from '../lib/stack.mjs';

const IMAGE = process.env.LODB_NGINX_IMAGE;
const API_HOST = 'api.localhost';
const KIB = 1024;
const BLOB = 'ab'.repeat(32);
const BLOCKED_AGENT = 'Mozilla/5.0 (X11; Linux aarch64) AppleWebKit/537.36 (KHTML, like Gecko) '
  + 'Chrome/139.0.0.0 Safari/537.36';

// Rates per minute: no token comes back while the checks run, so each burst is exact.
const LIMITS = {
  LODB_PAGES_RATE: '1r/m',
  LODB_PAGES_BURST: '3',
  LODB_API_READS_RATE: '1r/m',
  LODB_API_READS_BURST: '2',
  LODB_LEGACY_RATE: '1r/m',
  LODB_LEGACY_BURST: '6',
  LODB_SITEMAPS_RATE: '1r/m',
  LODB_SITEMAPS_BURST: '5',
  LODB_READYZ_RATE: '1r/m',
  LODB_READYZ_BURST: '1',
};

describe('nginx image', { skip: IMAGE ? false : 'LODB_NGINX_IMAGE is not set' }, () => {
  let stack;
  const get = (path, options) => send(stack.port, path, options);
  const cacheOf = async (path) => (await get(path)).headers['x-cache-status'];

  before(async () => {
    stack = await startStack(IMAGE, LIMITS);
  });

  after(() => stack?.stop());

  test('accepts its rendered configuration', async () => {
    const { stderr } = await stack.exec('nginx', '-t');
    assert.match(stderr, /test is successful/);
  });

  // Before the page and legacy checks: it spends one request of each burst (LIMITS).
  test('leaves a username that ends like a script to the profiles', async () => {
    assert.equal((await get('/en/u/kevin.bak')).status, 200);
    assert.equal((await get('/u/kevin.bak')).status, 301);
    assert.equal((await get('/api/profiles/kevin.ini')).status, 200);
    assert.equal((await get('/v1/profiles/kevin.env', { host: API_HOST })).status, 200);
    assert.equal((await get('/en/u/kevin/x.php')).status, 404);
  });

  test('refuses a client past its page burst with a 429 and Retry-After', async () => {
    assert.deepEqual(await statusesOf(stack.port, '/en/champions', 4), [200, 200, 200, 429]);
    const refused = await get('/en/items');
    assert.equal(refused.status, 429);
    assert.equal(refused.headers['retry-after'], '10');
    assert.equal(refused.headers['cache-control'], 'no-store');
  });

  test('limits the public reads of the API, never its other paths', async () => {
    const reads = await statusesOf(stack.port, '/api/catalog/16.1.1/en_US/champions', 4);
    assert.deepEqual(reads, [200, 200, 200, 429]);
    assert.equal((await get('/api/meta')).status, 429);
    // The API routes a path whatever its case, an encoded letter included: so does the limit.
    for (const path of ['/api/Catalog/16.1.1/en_US/champions', '/api/%53hare/abc']) {
      assert.equal((await get(path)).status, 429, path);
    }
    for (const path of ['/api/account/me', '/v1/champions', '/webhooks/stripe', '/build/a.js']) {
      assert.ok(!(await statusesOf(stack.port, path, 6)).includes(429), path);
    }
  });

  test('answers 404 itself for scripts, dumps and dotfiles, on both hosts', async () => {
    for (const path of ['/wp-login.php', '/admin/config.env', '/backup.sql', '/.git/config']) {
      const response = await get(path);
      assert.equal(response.status, 404, path);
      assert.equal(response.headers['x-cache-status'], undefined, path);
    }
    // Under /v1/, which the API host proxies: a 404 there comes from the guards alone.
    for (const path of ['/v1/index.php', '/v1/.env']) {
      assert.equal((await get(path, { host: API_HOST })).status, 404, path);
    }
  });

  test('refuses the blocked agent on both hosts', async () => {
    assert.equal((await get('/v1/champions', { agent: BLOCKED_AGENT })).status, 403);
    const onApiHost = await get('/v1/champions', { host: API_HOST, agent: BLOCKED_AGENT });
    assert.equal(onApiHost.status, 403);
  });

  test('exposes /readyz on the API host at its own rate', async () => {
    const statuses = await statusesOf(stack.port, '/readyz', 3, { host: API_HOST });
    assert.deepEqual(statuses, [200, 200, 429]);
  });

  test('caps request bodies at 128 KiB, the webhooks at 1 MiB', async () => {
    const post = async (path, size) =>
      (await get(path, { method: 'POST', body: Buffer.alloc(size) })).status;
    assert.equal(await post('/api/account/login', 128 * KIB), 200);
    assert.equal(await post('/api/account/login', 129 * KIB), 413);
    assert.equal(await post('/webhooks/stripe', 129 * KIB), 200);
    assert.equal(await post('/webhooks/stripe', 1025 * KIB), 413);
  });

  test('serves blobs alone from the storage, under a sandbox policy', async () => {
    await stack.exec('sh', '-c', 'mkdir -p /srv/storage/blobs && cd /srv/storage/blobs'
      + ` && printf x > ${BLOB}.png && printf x > ${BLOB}.svg && printf x > notes.txt`);
    const blob = await get(`/cdn/blobs/${BLOB}.png`);
    assert.equal(blob.status, 200);
    assert.equal(blob.headers['content-security-policy'], "default-src 'none'; sandbox");
    assert.equal(blob.headers['cross-origin-resource-policy'], 'cross-origin');
    assert.equal(blob.headers['cache-control'], 'public, max-age=31536000, immutable');
    for (const name of [`${BLOB}.svg`, `${BLOB}.PNG`, 'notes.txt', 'ab.png']) {
      assert.equal((await get(`/cdn/blobs/${name}`)).status, 404, name);
    }
    assert.equal((await get('/cdn/x')).status, 404);
  });

  test('keeps the crawler files once, whatever their query', async () => {
    assert.equal(await cacheOf('/robots.txt?utm_source=x'), 'MISS');
    assert.equal(await cacheOf('/robots.txt'), 'HIT');
    assert.equal(await cacheOf('/sitemaps/en/latest.xml'), 'MISS');
    assert.equal(await cacheOf('/sitemaps/en/latest.xml?page=2'), 'HIT');
    // The API receives the path as sent: an encoded slash names another file there (a 404
    // kept a minute), which must never answer for the decoded one.
    assert.equal(await cacheOf('/sitemaps/en%2Flatest.xml'), 'MISS');
  });

  test("keeps the former site's 301 per query, then holds a client to its rate", async () => {
    const moved = await get('/champions/Ahri?lang=fr_FR');
    assert.equal(moved.status, 301);
    assert.equal(moved.headers['x-cache-status'], 'MISS');
    assert.equal(await cacheOf('/champions/Ahri?lang=fr_FR'), 'HIT');
    assert.equal(await cacheOf('/champions/Ahri'), 'MISS');
    assert.deepEqual(await statusesOf(stack.port, '/champions', 4), [301, 301, 301, 429]);
    assert.equal((await get('/champions')).headers['retry-after'], '10');
  });

  test('holds a client sweeping the sitemaps to its rate', async () => {
    // Three of its burst of five went to the cache check above.
    const statuses = await statusesOf(stack.port, '/sitemaps/fr/latest.xml', 4);
    assert.deepEqual(statuses, [200, 200, 200, 429]);
  });
});
