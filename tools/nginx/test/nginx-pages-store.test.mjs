// Boots the built nginx image in front of the stub's page store (lib/upstream-stub.conf) and
// checks lot 3 of ADR 0011: a past patch's page comes from the store, then from the cache, with
// its own headers and none of S3's; every failure of the store, a query and a path a pass never
// writes reach the SSR server, which the store never sees; the rate holds across both. Switched
// off, nginx never asks the store; with a store whose name does not resolve, or whose
// certificate no authority signed, it starts and falls back. Needs Docker and the image's tag in
// LODB_NGINX_IMAGE (tools/nginx/README.md); skipped without it.
import assert from 'node:assert/strict';
import { after, before, describe, test } from 'node:test';
import { gunzipSync } from 'node:zlib';
import { send, statusesOf } from '../lib/client.mjs';
import { startStack } from '../lib/stack.mjs';

const IMAGE = process.env.LODB_NGINX_IMAGE;
// The stub's store: bucket lodb-pages, its pages on epoch 5, over plain HTTP on its alias. A
// page is kept from its first request, and each client gets a burst of four pages a minute.
const STORE = {
  LODB_EDGE_CIDR: '0.0.0.0/0',
  LODB_EDGE_SECRET: '',
  LODB_PAGES_STORE_ENABLED: '1',
  LODB_PAGES_STORE_SCHEME: 'http',
  LODB_PAGES_STORE_HOST: 'lodb-pages-store',
  LODB_PAGES_STORE_PORT: '9000',
  LODB_PAGES_STORE_BUCKET: 'lodb-pages',
  LODB_ARCHIVE_EPOCH: '4',
  LODB_ARCHIVE_SERVE_EPOCH: '5',
  LODB_ARCHIVE_CACHE_MIN_USES: '1',
  LODB_PAGES_RATE: '1r/m',
  LODB_PAGES_BURST: '4',
};
// A client's first page, then its burst.
const PAGES_BEFORE_REFUSAL = 1 + Number(STORE.LODB_PAGES_BURST);
const OBJECTS = '/lodb-pages/pages/5';
// The stub store holds the pages of 15.2.1; each other version stands for one of its failures.
const STORED = '/en/15.2.1/champions';
const FAILING = {
  missing: '/en/15.1.1/champions',
  forbidden: '/en/15.3.1/champions',
  throttled: '/en/15.4.1/champions',
  failed: '/en/15.5.1/champions',
  unavailable: '/en/15.6.1/champions',
  silent: '/en/15.7.1/champions',
};
// Answers with the headers nginx sent along with the key.
const ECHOED = '/en/15.8.1/champions';
// A page stored without its policy nor its type, and the policy nginx gives it instead.
const UNLABELLED = '/en/15.9.1/champions';
const FALLBACK_POLICY = "default-src 'self'; img-src 'self' data:; "
  + "style-src 'self' 'unsafe-inline'; object-src 'none'; frame-src 'none'; "
  + "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
// What the stub's SSR server answers for any past patch's page.
const RENDERED = { body: 'archive\n', policy: "script-src 'sha256-rendered'",
  cacheControl: 'public, max-age=3600, s-maxage=31536000', type: 'text/html' };
// What a stored page carries: its own headers, from its object's metadata, and the `archived`
// class's year, though the object still says the former class's week.
const STORED_HEADERS = {
  'content-type': 'text/html; charset=utf-8',
  'cache-control': 'public, max-age=3600, s-maxage=31536000',
  'content-security-policy': "script-src 'sha256-stored'",
  'content-security-policy-report-only': "require-trusted-types-for 'script'",
  'reporting-endpoints': 'csp="/api/csp-reports"',
  link: '<https://ddragon.leagueoflegends.com>; rel=preconnect',
  vary: 'Accept-Encoding',
};
// What a client may send that must never reach a third party, nor change what it answers.
const CLIENT_HEADERS = { cookie: 'lod_prefs=loc=fr', authorization: 'Bearer stolen',
  'x-amz-security-token': 'forged', traceparent:
  '00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01' };
// The store's read timeout (snippets/pages-store/request.conf) is 3 s; a fallback that waited
// on the SSR server's 30 s would take this long at least.
const SILENT_STORE_LIMIT_MS = 10_000;

// A client address of its own for each use: the burst of one never runs into another's.
let lastClient = 0;
const aClient = () => ({ forwardedFor: `203.0.113.${(lastClient += 1)}` });
// What a store adds of its own (snippets/pages-store/answer.conf), S3's and the local stack's.
const STORE_HEADER = /^(?:x-amzn?-|access-control-)/;
const storeHeadersOf = (answer) => Object.keys(answer.headers).filter((name) =>
  STORE_HEADER.test(name));
const sourceOf = (answer) =>
  [answer.headers['x-cache-status'], answer.headers['x-lodb-source']];

describe('nginx image in front of the page store',
  { skip: IMAGE ? false : 'LODB_NGINX_IMAGE is not set' }, () => {
    let stack;
    const get = (path, options) => send(stack.port, path, options);
    const keysOf = async () => {
      const { stdout } = await stack.exec('grep', '-rah', '^KEY: ', '/var/cache/nginx/archive');
      return stdout.split('\n').filter(Boolean);
    };

    before(async () => {
      stack = await startStack(IMAGE, STORE);
    });

    after(() => stack?.stop());

    test('accepts its rendered configuration', async () => {
      const { stderr } = await stack.exec('nginx', '-t');
      assert.match(stderr, /test is successful/);
    });

    test("serves a stored page from the store, then from its cache, with the page's headers",
      async () => {
        const visitor = aClient();
        const fetched = await get(STORED, { ...visitor, encoding: 'gzip' });
        const kept = await get(STORED, visitor);

        assert.equal(fetched.status, 200);
        assert.equal(fetched.headers['content-encoding'], 'gzip');
        assert.equal(gunzipSync(fetched.body).toString(), stack.storedPage);
        // A client without gzip gets it decompressed.
        assert.equal(kept.headers['content-encoding'], undefined);
        assert.equal(kept.body.toString(), stack.storedPage);
        assert.deepEqual([fetched, kept].map(sourceOf), [['MISS', 'store'], ['HIT', 'cache']]);
        for (const answer of [fetched, kept]) {
          for (const [name, value] of Object.entries(STORED_HEADERS)) {
            assert.equal(answer.headers[name], value, name);
          }
          assert.deepEqual(storeHeadersOf(answer), []);
          assert.equal(answer.headers['x-content-type-options'], 'nosniff');
        }
        assert.deepEqual(await stack.storeRequests(), [`GET ${OBJECTS}${STORED}`]);
        assert.ok((await keysOf()).includes(`KEY: 4.5|http://localhost${STORED}`));
      });

    test('sends the store a page\'s key with its own Host and none of the client\'s headers',
      async () => {
        const answer = await get(ECHOED,
          { ...aClient(), agent: 'Mozilla/5.0', headers: CLIENT_HEADERS });
        assert.equal(answer.body.toString(), '[lodb-pages-store][]\n');
      });

    test("falls back to the SSR server on each failure of the store, never showing its answer",
      async () => {
        for (const [failure, path] of Object.entries(FAILING)) {
          const started = Date.now();
          const answer = await get(path, aClient());
          assert.equal(answer.status, 200, failure);
          assert.equal(answer.body.toString(), RENDERED.body, failure);
          assert.deepEqual(sourceOf(answer), ['MISS', 'ssr'], failure);
          assert.equal(answer.headers['content-security-policy'], RENDERED.policy, failure);
          assert.equal(answer.headers['cache-control'], RENDERED.cacheControl, failure);
          assert.deepEqual(storeHeadersOf(answer), [], failure);
          assert.ok(Date.now() - started < SILENT_STORE_LIMIT_MS, failure);
        }
      });

    test('keeps a page the SSR server rendered, with its own headers', async () => {
      const visitor = aClient();
      const rendered = await get(FAILING.missing.replace('/en/', '/fr/'), visitor);
      const kept = await get(FAILING.missing.replace('/en/', '/fr/'), visitor);

      assert.deepEqual([rendered, kept].map(sourceOf), [['MISS', 'ssr'], ['HIT', 'cache']]);
      assert.equal(kept.headers['content-security-policy'], RENDERED.policy);
      assert.equal(kept.headers['cache-control'], RENDERED.cacheControl);
      assert.equal(kept.headers['content-type'], RENDERED.type);
      assert.equal(kept.body.toString(), RENDERED.body);
    });

    test('gives a stored page without its policy nor its type a strict policy and its type',
      async () => {
        const visitor = aClient();
        const answers = [await get(UNLABELLED, visitor), await get(UNLABELLED, visitor)];

        assert.deepEqual(answers.map(sourceOf), [['MISS', 'store'], ['HIT', 'cache']]);
        for (const answer of answers) {
          assert.equal(answer.body.toString(), stack.storedPage);
          assert.equal(answer.headers['content-type'], STORED_HEADERS['content-type']);
          assert.equal(answer.headers['content-security-policy'], FALLBACK_POLICY);
        }
      });

    test('asks the SSR server alone for a query, another method or a path no pass writes',
      async () => {
        const before = await stack.storeRequests();
        const requests = [
          [`${STORED}?page=2`],
          [`${STORED}/`],
          ['/en/15.2.1/items/1036-long%2Dsword'],
          ['/en/15.2.1/champions/../items'],
          ['/en/15.2.1//champions'],
          ['/en/15.2.1/patches'],
          [STORED, { method: 'POST', body: Buffer.from('x') }],
        ];
        for (const [path, options] of requests) {
          const answer = await get(path, { ...aClient(), ...options });
          assert.equal(answer.body.toString(), RENDERED.body, path);
          assert.equal(answer.headers['x-lodb-source'], 'ssr', path);
        }
        // A last request that reaches the store: the ones above would be logged before it.
        await get(FAILING.forbidden.replace('/en/', '/de/'), aClient());
        assert.deepEqual((await stack.storeRequests()).slice(before.length),
          [`GET ${OBJECTS}/de/15.3.1/champions`]);
      });

    test("holds a client to the pages' rate on both ways, apart from the store's own 429",
      async () => {
        const visitor = aClient();
        // The first page and the burst: from the store, a query the SSR server renders, the
        // SSR server after the store's 429.
        const paths = [STORED, `${STORED}?page=3`, FAILING.throttled, STORED, `${STORED}?page=4`];
        const statuses = [];
        for (const path of paths) {
          statuses.push((await get(path, visitor)).status);
        }
        assert.deepEqual(statuses, Array(PAGES_BEFORE_REFUSAL).fill(200));
        for (const path of [STORED, `${STORED}?page=5`]) {
          const refused = await get(path, visitor);
          assert.equal(refused.status, 429, path);
          assert.equal(refused.headers['retry-after'], '10', path);
          assert.equal(refused.headers['cache-control'], 'no-store', path);
        }
      });
  });

describe('nginx image with the page store switched off',
  { skip: IMAGE ? false : 'LODB_NGINX_IMAGE is not set' }, () => {
    let stack;

    before(async () => {
      stack = await startStack(IMAGE, { ...STORE, LODB_PAGES_STORE_ENABLED: '0' });
    });

    after(() => stack?.stop());

    test('renders every past patch\'s page with the SSR server, never asking the store',
      async () => {
        const visitor = aClient();
        const statuses = await statusesOf(stack.port, STORED, 2, visitor);
        const kept = await send(stack.port, STORED, visitor);

        assert.deepEqual(statuses, [200, 200]);
        assert.equal(kept.body.toString(), RENDERED.body);
        assert.equal(kept.headers['x-lodb-source'], 'cache');
        assert.deepEqual(await stack.storeRequests(), []);
      });
  });

describe('nginx image with a page store it cannot trust or find',
  { skip: IMAGE ? false : 'LODB_NGINX_IMAGE is not set' }, () => {
    const stores = {
      // The stub's TLS port, under a certificate that no authority signed.
      untrusted: { LODB_PAGES_STORE_SCHEME: 'https', LODB_PAGES_STORE_PORT: '9443' },
      // As on a stack without network or DNS: nginx must start all the same.
      unresolved: { LODB_PAGES_STORE_HOST: 'lodb-pages-store.invalid' },
    };

    for (const [store, settings] of Object.entries(stores)) {
      test(`starts and renders the pages with the SSR server: ${store} store`, async () => {
        const stack = await startStack(IMAGE, { ...STORE, ...settings });
        try {
          const answer = await send(stack.port, STORED, aClient());
          assert.equal(answer.body.toString(), RENDERED.body);
          assert.equal(answer.headers['x-lodb-source'], 'ssr');
          assert.deepEqual(await stack.storeRequests(), []);
        } finally {
          await stack.stop();
        }
      });
    }
  });
