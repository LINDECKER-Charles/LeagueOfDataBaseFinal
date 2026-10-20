// Boots the built nginx image behind stub upstreams and checks its bounds: the edge's header,
// rates per client, body sizes, the paths answered by nginx alone, the blobs and the caches.
// Needs Docker and the image's tag in LODB_NGINX_IMAGE (tools/nginx/README.md); skipped
// without it.
import assert from 'node:assert/strict';
import { after, before, describe, test } from 'node:test';
import { gunzipSync } from 'node:zlib';
import { send, statusesOf } from '../lib/client.mjs';
import { startStack } from '../lib/stack.mjs';

const IMAGE = process.env.LODB_NGINX_IMAGE;
const API_HOST = 'api.localhost';
const KIB = 1024;
const BLOB = 'ab'.repeat(32);
// What the deploy job generates: 32 random bytes in hexadecimal.
const EDGE_SECRET = '5e'.repeat(32);
const NGINX_UID = '101';
const DEPLOY_EDGE_CHECKS = [['localhost', '/cdn/blobs/edge-check'], [API_HOST, '/edge-check']];
// Paths that nginx's maps, which ignore case, or a regex's `$` would take for /healthz.
const HEALTHZ_LOOKALIKES = ['/HEALTHZ', '/%48ealthz', '/healthz%0A', '/healthz/'];
// W3C traceparent of version 00, nothing appended: version, trace id, parent id, flags.
const TRACEPARENT = /^00-([0-9a-f]{32})-([0-9a-f]{16})-00$/;
const CLIENT_TRACEPARENT = '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01';
// What the stub's API answers for the sitemap index, llms.txt and an IndexNow key file
// (lib/upstream-stub.conf).
const CRAWLER_FILE = 'crawler file\n';
const BLOCKED_AGENT = 'Mozilla/5.0 (X11; Linux aarch64) AppleWebKit/537.36 (KHTML, like Gecko) '
  + 'Chrome/139.0.0.0 Safari/537.36';
// Refused on a stack that is never indexed only (nginx-noindex.test.mjs).
const AI_CRAWLER = 'meta-externalagent/1.1 '
  + '(+https://developers.facebook.com/docs/sharing/webmasters/crawler)';
// A client inside the container asks for the stub's large answer, then stops reading for a
// while (its output goes to a `sleep`). Meanwhile, the script prints the temporary files that
// nginx's processes hold open: nginx unlinks them as it opens them, so only /proc names them.
const SLOW_READER_SECONDS = 6;
const SPILL_POLLS = 5;
const SLOW_READ = [
  `request='GET /api/large HTTP/1.1\\r\\nHost: localhost\\r\\nX-LoDb-Edge: ${EDGE_SECRET}`
    + `\\r\\nConnection: close\\r\\n\\r\\n'`,
  `( (printf "$request"; sleep ${SLOW_READER_SECONDS}) | nc 127.0.0.1 8080`
    + ` | sleep ${SLOW_READER_SECONDS} ) >/dev/null 2>&1 &`,
  `for poll in $(seq ${SPILL_POLLS}); do`,
  '  sleep 1',
  '  spilled="$(for fd in /proc/[0-9]*/fd/*; do readlink "$fd"; done 2>/dev/null \\',
  "    | grep ' (deleted)$' | grep 'temp/' || true)\"",
  '  [ -z "$spilled" ] || break',
  'done',
  'echo "$spilled"',
  'wait',
].join('\n');

// Rates per minute: no token comes back while the checks run, so each burst is exact. Any peer
// may state the client address, so that a check of the caches speaks for a client of its own
// (`forwardedFor`) and leaves the bursts of the others whole. The first parts of the page
// caches' keys are named, to be found in the stored keys; a past patch's page waits for a
// third request rather than the image's second, to show where that count comes from. The
// edge's secret is set, as on a host: every check sends it unless it says otherwise.
const LIMITS = {
  LODB_EDGE_CIDR: '0.0.0.0/0',
  LODB_EDGE_SECRET: EDGE_SECRET,
  LODB_REVISION: 'r1',
  LODB_ARCHIVE_EPOCH: 'e7',
  LODB_ARCHIVE_CACHE_MIN_USES: '3',
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
  const asEdge = (options) => ({ edge: EDGE_SECRET, ...options });
  const get = (path, options) => send(stack.port, path, asEdge(options));
  const statusesFrom = (path, count, options) =>
    statusesOf(stack.port, path, count, asEdge(options));
  const cacheOf = async (path) => (await get(path)).headers['x-cache-status'];
  // The keys a cache folder holds, as nginx wrote them into its files.
  const keysOf = async (folder) => {
    const { stdout } = await stack.exec('grep', '-rah', '^KEY: ', `/var/cache/nginx/${folder}`);
    return stdout.split('\n').filter(Boolean);
  };

  before(async () => {
    stack = await startStack(IMAGE, LIMITS);
  });

  after(() => stack?.stop());

  test('accepts its rendered configuration', async () => {
    const { stderr } = await stack.exec('nginx', '-t');
    assert.match(stderr, /test is successful/);
  });

  test('runs unprivileged, without any capability, on a read-only root', async () => {
    const { stdout } = await stack.exec('sh', '-c',
      'id -u; stat -c %U /proc/1; grep -E "^(CapEff|CapBnd|NoNewPrivs):" /proc/1/status');
    const [uid, master, ...status] = stdout.trim().split('\n');
    assert.equal(uid, NGINX_UID);
    assert.equal(master, 'nginx');
    assert.deepEqual(status.map((line) => line.replace(/\s+/g, ' ')),
      ['CapEff: 0000000000000000', 'CapBnd: 0000000000000000', 'NoNewPrivs: 1']);
    await assert.rejects(stack.exec('touch', '/etc/nginx/nginx.conf'), /Read-only file system/);
  });

  test('refuses on every host a request the edge did not send, /healthz aside', async () => {
    for (const host of ['localhost', API_HOST, 'www.localhost']) {
      for (const edge of [undefined, 'f'.repeat(EDGE_SECRET.length), EDGE_SECRET.slice(1)]) {
        const refused = await get('/v1/champions', { host, edge });
        assert.equal(refused.status, 403, `${host} ${edge}`);
        assert.ok(!refused.body.toString().includes(EDGE_SECRET));
      }
    }
    for (const host of ['localhost', API_HOST]) {
      assert.equal((await get('/healthz', { host, edge: undefined })).status, 200, host);
      // `location = /healthz` misses these: they would reach `location /` without the header.
      for (const path of HEALTHZ_LOOKALIKES) {
        assert.equal((await get(path, { host, edge: undefined })).status, 403, host + path);
      }
    }
    assert.equal((await get('/v1/champions', { host: 'www.localhost' })).status, 301);
  });

  test('writes its own redirects with a relative Location, never its port', async () => {
    const moved = await get('/api');
    assert.equal(moved.status, 301);
    assert.equal(moved.headers.location, '/api/');
  });

  test("keeps the edge's header and a client's prerender marker from the upstreams", async () => {
    const answer = await get('/api/edge-header');
    const page = await get('/en/prerender-header',
      { forwardedFor: '203.0.113.4', headers: { 'x-lodb-prerender': '1' } });
    assert.deepEqual([answer, page].map(({ body }) => body.toString()), ['[]\n', '[]\n']);
    assert.ok(!JSON.stringify(answer.headers).includes(EDGE_SECRET));
  });

  // In the bounded tmpfs of /tmp, a few slow readers of large answers would fill it, and every
  // answer or request body that outgrows the buffers would then fail.
  test('keeps what a slow client has not read on its volume, never in /tmp', async () => {
    const { stdout } = await stack.exec('sh', '-c', SLOW_READ);
    const spilled = stdout.split('\n').filter(Boolean);
    assert.ok(spilled.length > 0, 'nginx holds part of the answer in a temporary file');
    for (const file of spilled) {
      assert.match(file, /^\/var\/cache\/nginx\/temp\/proxy\//);
    }
  });

  // What the deploy job reads through the edge (_deploy.yml): nginx's own 404 on each site.
  test("answers the deploy's checks through the edge itself, and only with its header",
    async () => {
      for (const [host, path] of DEPLOY_EDGE_CHECKS) {
        assert.equal((await get(path, { host })).status, 404, host);
        assert.equal((await get(path, { host, edge: undefined })).status, 403, host);
      }
    });

  test('starts a W3C trace for each request it proxies, from its own id', async () => {
    const traces = [];
    for (let sent = 0; sent < 2; sent += 1) {
      traces.push((await get('/api/trace')).headers['x-seen-traceparent']);
    }

    for (const trace of traces) {
      const [, traceId, parentId] = TRACEPARENT.exec(trace) ?? [];
      assert.ok(traceId, trace);
      assert.equal(parentId, traceId.slice(0, 16));
    }
    assert.notEqual(traces[0], traces[1]);
  });

  test("replaces a client's trace context rather than continuing it", async () => {
    const client = { headers: {
      traceparent: CLIENT_TRACEPARENT,
      tracestate: 'vendor=1',
      baggage: 'user=someone',
    } };

    const seen = (await get('/api/trace', client)).headers;

    assert.match(seen['x-seen-traceparent'], TRACEPARENT);
    assert.ok(!seen['x-seen-traceparent'].includes(CLIENT_TRACEPARENT.split('-')[1]));
    assert.equal(seen['x-seen-tracestate'], undefined);
    assert.equal(seen['x-seen-baggage'], undefined);
  });

  test('hands the SSR server the trace of the page request', async () => {
    const page = await get('/en/trace', { forwardedFor: '203.0.113.9' });

    assert.match(page.headers['x-seen-traceparent'], TRACEPARENT);
  });

  test('denies the unused device features and isolates the pages from their opener',
    async () => {
      const answers = [await get('/healthz'), await get('/api/trace'),
        await get('/v1/champions', { host: API_HOST }), await get('/cdn/x')];
      for (const answer of answers) {
        assert.equal(answer.headers['cross-origin-opener-policy'], 'same-origin');
        const features = answer.headers['permissions-policy'].split(', ');
        for (const feature of ['camera=()', 'microphone=()', 'geolocation=()', 'payment=()']) {
          assert.ok(features.includes(feature), feature);
        }
        assert.ok(!features.some((feature) => feature.startsWith('fullscreen')));
      }
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
    assert.deepEqual(await statusesFrom('/en/champions', 4), [200, 200, 200, 429]);
    const refused = await get('/en/items');
    assert.equal(refused.status, 429);
    assert.equal(refused.headers['retry-after'], '10');
    assert.equal(refused.headers['cache-control'], 'no-store');
  });

  test('limits the public reads of the API, never its other paths', async () => {
    const reads = await statusesFrom('/api/catalog/16.1.1/en_US/champions', 4);
    assert.deepEqual(reads, [200, 200, 200, 429]);
    assert.equal((await get('/api/meta')).status, 429);
    // The API routes a path whatever its case, an encoded letter included: so does the limit.
    for (const path of ['/api/Catalog/16.1.1/en_US/champions', '/api/%53hare/abc']) {
      assert.equal((await get(path)).status, 429, path);
    }
    for (const path of ['/api/account/me', '/v1/champions', '/webhooks/stripe', '/build/a.js']) {
      assert.ok(!(await statusesFrom(path, 6)).includes(429), path);
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

  test('refuses the blocked agent on both hosts, and that agent alone', async () => {
    assert.equal((await get('/v1/champions', { agent: BLOCKED_AGENT })).status, 403);
    const onApiHost = await get('/v1/champions', { host: API_HOST, agent: BLOCKED_AGENT });
    assert.equal(onApiHost.status, 403);
    // An exact string, never a pattern: the next build of the same browser goes through.
    const nextBuild = BLOCKED_AGENT.replace('Chrome/139.0.0.0', 'Chrome/140.0.0.0');
    assert.equal((await get('/v1/champions', { agent: nextBuild })).status, 200);
  });

  test('lets the AI crawlers through where pages are indexed', async () => {
    const answer = await get('/v1/champions', { agent: AI_CRAWLER });
    assert.equal(answer.status, 200);
    assert.equal(answer.headers['x-robots-tag'], undefined);
  });

  test('exposes /readyz on the API host at its own rate', async () => {
    const statuses = await statusesFrom('/readyz', 3, { host: API_HOST });
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

  test('keeps one compressed copy of a page for every client', async () => {
    const client = { forwardedFor: '203.0.113.1' };
    const compressed = await get('/en/about', { ...client, encoding: 'gzip' });
    const plain = await get('/en/about', client);
    const browser = await get('/en/about', { ...client, encoding: 'gzip, deflate, br, zstd' });

    assert.equal(compressed.headers['x-cache-status'], 'MISS');
    assert.equal(compressed.headers['content-encoding'], 'gzip');
    assert.equal(gunzipSync(compressed.body).toString(), 'page\n');
    assert.equal(plain.headers['x-cache-status'], 'HIT');
    assert.equal(plain.headers['content-encoding'], undefined);
    assert.equal(plain.body.toString(), 'page\n');
    assert.equal(browser.headers['x-cache-status'], 'HIT');
    assert.deepEqual(browser.body, compressed.body);
    for (const response of [compressed, plain, browser]) {
      assert.equal(response.headers.vary, 'Accept-Encoding');
    }
  });

  test("keeps a past patch's page from its third request here, under a key without revision",
    async () => {
      const client = { forwardedFor: '203.0.113.2' };
      const path = '/en/15.1.1/champions';
      const misses = [];
      for (let attempt = 0; attempt < 3; attempt += 1) {
        misses.push(await get(path, { ...client, encoding: 'gzip' }));
      }
      const kept = await get(path, client);

      assert.deepEqual([...misses, kept].map((answer) => answer.headers['x-cache-status']),
        ['MISS', 'MISS', 'MISS', 'HIT']);
      assert.equal(gunzipSync(misses[2].body).toString(), 'archive\n');
      assert.equal(kept.body.toString(), 'archive\n');
      assert.equal(kept.headers.vary, 'Accept-Encoding');
      assert.deepEqual(await keysOf('archive'), [`KEY: e7|http://localhost${path}`]);
      assert.ok((await keysOf('pages')).includes('KEY: r1|http://localhost/en/about'));
    });

  test("holds a client to the pages' rate on a past patch too", async () => {
    const statuses = await statusesFrom('/fr/14.24.1/items', 5,
      { forwardedFor: '203.0.113.3' });
    assert.deepEqual(statuses, [200, 200, 200, 200, 429]);
  });

  test('keeps a bundle under its name alone, whatever the host, but never a 404', async () => {
    assert.equal(await cacheOf('/build/main-ABCD1234.js'), 'MISS');
    const kept = await get('/build/main-ABCD1234.js', { host: 'test.example.com' });
    assert.equal(kept.headers['x-cache-status'], 'HIT');
    assert.equal(kept.headers['cache-control'], 'public, max-age=31536000, immutable');
    assert.equal(kept.headers['x-content-type-options'], 'nosniff');
    for (let attempt = 0; attempt < 2; attempt += 1) {
      const gone = await get('/build/gone-ABCD1234.js');
      assert.equal(gone.status, 404);
      assert.equal(gone.headers['x-cache-status'], 'MISS');
    }
  });

  test('keeps a file of /build/ opened directly from running anything', async () => {
    for (const path of ['/build/main-ABCD1234.js', '/build/media/logo-ABCD1234.svg',
      '/build/gone-ABCD1234.js']) {
      const answer = await get(path);
      assert.equal(answer.headers['content-security-policy'], "default-src 'none'; sandbox",
        path);
    }
  });

  test('keeps the crawler files once, whatever their query', async () => {
    assert.equal(await cacheOf('/robots.txt?utm_source=x'), 'MISS');
    assert.equal(await cacheOf('/robots.txt'), 'HIT');
    assert.equal(await cacheOf('/sitemaps/en/latest.xml'), 'MISS');
    assert.equal(await cacheOf('/sitemaps/en/latest.xml?page=2'), 'HIT');
    // The API receives the path as sent: an encoded slash names another file there (a 404
    // kept a minute), which must never answer for the decoded one.
    assert.equal(await cacheOf('/sitemaps/en%2Flatest.xml'), 'MISS');
    // On a volume, under the revision: a deploy starts them over, as the pages.
    assert.ok((await keysOf('seo')).includes('KEY: r1|http://localhost/robots.txt'));
    // The API's too, never the SSR server's, which knows none of them.
    for (const path of ['/sitemap.xml', '/llms.txt', '/5e5e5e5e5e5e5e5e.txt']) {
      const first = await get(path);
      assert.equal(first.body.toString(), CRAWLER_FILE, path);
      assert.equal(first.headers['x-cache-status'], 'MISS', path);
      assert.equal(await cacheOf(path), 'HIT', path);
    }
  });

  test("keeps the former site's 301 per query, then holds a client to its rate", async () => {
    const moved = await get('/champions/Ahri?lang=fr_FR');
    assert.equal(moved.status, 301);
    assert.equal(moved.headers['x-cache-status'], 'MISS');
    assert.equal(await cacheOf('/champions/Ahri?lang=fr_FR'), 'HIT');
    assert.equal(await cacheOf('/champions/Ahri'), 'MISS');
    const legacyKey = 'KEY: r1|http://localhost/champions/Ahri?lang=fr_FR';
    assert.ok((await keysOf('legacy')).includes(legacyKey));
    assert.deepEqual(await statusesFrom('/champions', 4), [301, 301, 301, 429]);
    assert.equal((await get('/champions')).headers['retry-after'], '10');
  });

  test('holds a client sweeping the sitemaps to its rate', async () => {
    // Three of its burst of five went to the cache check above.
    const statuses = await statusesFrom('/sitemaps/fr/latest.xml', 4);
    assert.deepEqual(statuses, [200, 200, 200, 429]);
  });
});
