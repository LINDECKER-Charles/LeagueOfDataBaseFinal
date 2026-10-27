// Boots the built nginx image in front of the stub's page store (lib/upstream-stub.conf) and
// checks lot 3 of ADR 0011: a past patch's page comes from the store at every request, never
// kept (amendment of 2026-10-27), with its own headers and none of S3's; every failure of the
// store, a query and a path a pass never writes reach the SSR server, whose render is kept, and
// which the store never sees, and so does the latest patch pinned in the path; the rate holds
// across both; a bundle the SSR server no longer has comes from the
// store, under the same rate. Switched off, nginx never asks the store; a store it cannot trust
// or find is nginx-pages-store-unreachable.test.mjs's, how long a page is kept by its class
// nginx-pages-store-classes.test.mjs's. Needs Docker and the image's tag in LODB_NGINX_IMAGE
// (tools/nginx/README.md); skipped without it.
import assert from 'node:assert/strict';
import { after, before, describe, test } from 'node:test';
import { gunzipSync } from 'node:zlib';
import { send, statusesOf } from '../../lib/client.mjs';
import { startStack } from '../../lib/stack.mjs';

const IMAGE = process.env.LODB_NGINX_IMAGE;
// The stub's store: bucket lodb-pages, its pages on epoch 5, over plain HTTP on its alias. A
// page the SSR server renders is kept from its first request, and each client gets a burst of
// four pages a minute.
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
// The latest patch, as the stub's API states it (/api/meta), pinned in the path: the SSR server
// redirects it to the short URL, and no pass stores it. A patch that only starts like it does
// not stand for it.
const LATEST_PINNED = ['/en/15.10.1/champions/Ahri', '/zh-hant/15.10.1/items',
  '/fr/15.10.1/runes/8005-press-the-attack'];
const LATEST_REDIRECT = { status: 301, cacheControl: 'public, max-age=0, s-maxage=60' };
const LATEST_LOOKALIKE = '/en/15.10.10/champions';
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
// Bundles (lib/upstream-stub.conf): one the SSR server no longer has and the store keeps, one
// neither has (the store answers 403), one only the SSR server has, and a name no pass writes,
// which nginx resolves to the first one's.
const PRUNED_BUNDLE = '/build/old-ABCD1234.js';
const GONE_BUNDLE = '/build/gone-ABCD1234.js';
const CURRENT_BUNDLE = '/build/main-ABCD1234.js';
const DOTTED_BUNDLE = '/build/media/../old-ABCD1234.js';
// The SSR server's answer to a name no build has (src/server), which nginx keeps whatever the
// store says: a plain text, never nginx's HTML page nor S3's error document.
const SSR_NOT_FOUND = { body: 'Not Found', type: 'text/plain; charset=utf-8' };
const STORED_BUNDLE = { body: 'stored bundle\n', type: 'text/javascript',
  cacheControl: 'public, max-age=31536000, immutable', policy: "default-src 'none'; sandbox" };
// The store's read timeout (snippets/pages-store/request.conf) is 3 s; a fallback that waited
// on the SSR server's 30 s would take this long at least.
const SILENT_STORE_LIMIT_MS = 10_000;

// A client address of its own for each use: the burst of one never runs into another's.
let lastClient = 0;
const aClient = () => ({ forwardedFor: `203.0.113.${(lastClient += 1)}` });
// What a store adds of its own (snippets/pages-store/answer.conf), S3's and the local stack's,
// the validators and ranges it states of its object included.
const STORE_HEADER = /^(?:x-amzn?-|access-control-|etag$|last-modified$|accept-ranges$)/;
// The validators the store states of an object, as its headers name them, and the conditional
// request a client holding each one sends.
const VALIDATORS = { etag: 'if-none-match', 'last-modified': 'if-modified-since' };
const storeHeadersOf = (answer) => Object.keys(answer.headers).filter((name) =>
  STORE_HEADER.test(name));
const sourceOf = (answer) =>
  [answer.headers['x-cache-status'], answer.headers['x-lodb-source']];
// A page the store answered: no cache looked it up, none kept it.
const STORE_ANSWER = [undefined, 'store'];

describe('nginx image in front of the page store',
  { skip: IMAGE ? false : 'LODB_NGINX_IMAGE is not set' }, () => {
    let stack;
    const get = (path, options) => send(stack.port, path, options);
    // The keys of the files the past patches' cache holds, none at all included.
    const keysOf = async () => {
      const { stdout } = await stack.exec('sh', '-c',
        "grep -rah '^KEY: ' /var/cache/nginx/archive || true");
      return stdout.split('\n').filter(Boolean);
    };
    // What a client would send, holding the validators the store gives a page's object, read
    // from the store itself (busybox's wget prints the headers it got).
    const conditionalHeadersOf = async (path) => {
      const object = `http://${STORE.LODB_PAGES_STORE_HOST}:${STORE.LODB_PAGES_STORE_PORT}`
        + `${OBJECTS}${path}`;
      const { stderr } = await stack.exec('wget', '-S', '-O', '/dev/null', object);
      return Object.entries(VALIDATORS).map(([validator, condition]) => {
        const value = new RegExp(`^\\s*${validator}: (.+)$`, 'im').exec(stderr)?.[1].trim();
        assert.ok(value, validator);
        return { [condition]: value };
      });
    };

    before(async () => {
      stack = await startStack(IMAGE, STORE);
    });

    after(() => stack?.stop());

    test('accepts its rendered configuration', async () => {
      const { stderr } = await stack.exec('nginx', '-t');
      assert.match(stderr, /test is successful/);
    });

    test("serves a stored page from the store at every request, with the page's headers",
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
        assert.deepEqual([fetched, kept].map(sourceOf), [STORE_ANSWER, STORE_ANSWER]);
        for (const answer of [fetched, kept]) {
          for (const [name, value] of Object.entries(STORED_HEADERS)) {
            assert.equal(answer.headers[name], value, name);
          }
          assert.deepEqual(storeHeadersOf(answer), []);
          assert.equal(answer.headers['x-content-type-options'], 'nosniff');
        }
        assert.deepEqual(await stack.storeRequests(), Array(2).fill(`GET ${OBJECTS}${STORED}`));
        assert.ok(!(await keysOf()).includes(`KEY: 4.5|http://localhost${STORED}`));
      });

    test('answers a HEAD and a conditional request with the page, under no validator of S3',
      async () => {
        const path = STORED.replace('/en/', '/de/');
        const head = await get(path, { ...aClient(), method: 'HEAD' });
        const conditional = [];
        for (const headers of await conditionalHeadersOf(path)) {
          conditional.push([await get(path, { ...aClient(), headers }),
            await get(path, { ...aClient(), encoding: 'gzip', headers })]);
        }
        // Never a range of the compressed bytes, which are not the page.
        const ranged = await get(path, { ...aClient(), encoding: 'gzip',
          headers: { range: 'bytes=0-3' } });

        assert.equal(head.status, 200);
        assert.equal(head.body.length, 0);
        assert.deepEqual(sourceOf(head), STORE_ANSWER);
        assert.equal(head.headers['content-type'], STORED_HEADERS['content-type']);
        assert.equal(head.headers['cache-control'], STORED_HEADERS['cache-control']);
        for (const [plain, compressed] of conditional) {
          assert.deepEqual([plain, compressed].map(sourceOf), [STORE_ANSWER, STORE_ANSWER]);
          assert.deepEqual([plain.status, compressed.status], [200, 200]);
          assert.equal(plain.body.toString(), stack.storedPage);
          assert.equal(gunzipSync(compressed.body).toString(), stack.storedPage);
        }
        assert.equal(ranged.status, 200);
        assert.equal(gunzipSync(ranged.body).toString(), stack.storedPage);
        for (const answer of [head, ...conditional.flat(), ranged]) {
          assert.deepEqual(storeHeadersOf(answer), []);
        }
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
      const path = FAILING.missing.replace('/en/', '/fr/');
      const rendered = await get(path, visitor);
      const kept = await get(path, visitor);

      assert.deepEqual([rendered, kept].map(sourceOf), [['MISS', 'ssr'], ['HIT', 'cache']]);
      assert.ok((await keysOf()).includes(`KEY: 4.5|http://localhost${path}`));
      assert.equal(kept.headers['content-security-policy'], RENDERED.policy);
      assert.equal(kept.headers['cache-control'], RENDERED.cacheControl);
      assert.equal(kept.headers['content-type'], RENDERED.type);
      assert.equal(kept.body.toString(), RENDERED.body);
    });

    test('gives a stored page without its policy nor its type a strict policy and its type',
      async () => {
        const visitor = aClient();
        const answers = [await get(UNLABELLED, visitor), await get(UNLABELLED, visitor)];

        assert.deepEqual(answers.map(sourceOf), [STORE_ANSWER, STORE_ANSWER]);
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
          ['/en/15.2.1/changes/items'],
          ['/en/15.2.1/changesets'],
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

    test('leaves the latest patch pinned in the path to the SSR server, never asking the store',
      async () => {
        const before = await stack.storeRequests();
        for (const path of LATEST_PINNED) {
          const answer = await get(path, aClient());
          assert.equal(answer.status, LATEST_REDIRECT.status, path);
          assert.equal(answer.headers['cache-control'], LATEST_REDIRECT.cacheControl, path);
          assert.deepEqual(sourceOf(answer), ['MISS', 'ssr'], path);
        }
        const lookalike = await get(LATEST_LOOKALIKE, aClient());

        assert.equal(lookalike.body.toString(), RENDERED.body);
        assert.deepEqual((await stack.storeRequests()).slice(before.length),
          [`GET ${OBJECTS}${LATEST_LOOKALIKE}`]);
      });

    test('looks a bundle up in the store only when the SSR server has no such name', async () => {
      const visitor = aClient();
      const before = await stack.storeRequests();
      const missed = await get(DOTTED_BUNDLE, visitor);
      const fetched = await get(PRUNED_BUNDLE, visitor);
      const kept = await get(PRUNED_BUNDLE, { ...visitor, host: 'test.example.com' });
      const gone = [await get(GONE_BUNDLE, visitor), await get(GONE_BUNDLE, visitor)];

      assert.equal(missed.status, 404);
      assert.deepEqual([fetched, kept].map((answer) => answer.headers['x-cache-status']),
        ['MISS', 'HIT']);
      for (const answer of [fetched, kept]) {
        assert.equal(answer.status, 200);
        assert.equal(answer.body.toString(), STORED_BUNDLE.body);
        assert.equal(answer.headers['content-type'], STORED_BUNDLE.type);
        assert.equal(answer.headers['cache-control'], STORED_BUNDLE.cacheControl);
        assert.deepEqual(storeHeadersOf(answer), []);
      }
      for (const answer of gone) {
        assert.equal(answer.status, 404);
        assert.equal(answer.headers['x-cache-status'], 'MISS');
        assert.ok(!answer.body.toString().includes('AccessDenied'));
      }
      for (const answer of [missed, ...gone]) {
        assert.equal(answer.body.toString(), SSR_NOT_FOUND.body);
        assert.equal(answer.headers['content-type'], SSR_NOT_FOUND.type);
      }
      for (const answer of [missed, fetched, ...gone]) {
        assert.equal(answer.headers['content-security-policy'], STORED_BUNDLE.policy);
        assert.equal(answer.headers['x-content-type-options'], 'nosniff');
      }
      assert.equal((await get(CURRENT_BUNDLE)).body.toString(), 'bundle\n');
      assert.deepEqual((await stack.storeRequests()).slice(before.length),
        [PRUNED_BUNDLE, GONE_BUNDLE, GONE_BUNDLE].map((path) => `GET /lodb-pages${path}`));
    });

    test("holds a client to the pages' rate on the store's bundles, its misses included",
      async () => {
        const visitor = aClient();
        const before = await stack.storeRequests();
        // The SSR server's 404s and the hits of the bundles' cache never count.
        await get(CURRENT_BUNDLE, visitor);
        await get(DOTTED_BUNDLE, visitor);
        const missing = [];
        for (let request = 0; request < PAGES_BEFORE_REFUSAL; request += 1) {
          missing.push((await get(GONE_BUNDLE, visitor)).status);
        }
        const refused = await get(GONE_BUNDLE, visitor);

        assert.deepEqual(missing, Array(PAGES_BEFORE_REFUSAL).fill(404));
        assert.equal(refused.status, 429);
        assert.equal(refused.headers['retry-after'], '10');
        assert.equal(refused.headers['cache-control'], 'no-store');
        assert.deepEqual((await stack.storeRequests()).slice(before.length),
          Array(PAGES_BEFORE_REFUSAL).fill(`GET /lodb-pages${GONE_BUNDLE}`));
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

    test('answers 404 for a bundle the SSR server no longer has, never asking the store',
      async () => {
        const gone = await send(stack.port, PRUNED_BUNDLE);
        assert.equal(gone.status, 404);
        assert.equal(gone.body.toString(), SSR_NOT_FOUND.body);
        assert.equal(gone.headers['content-type'], SSR_NOT_FOUND.type);
        assert.equal(gone.headers['content-security-policy'], STORED_BUNDLE.policy);
        assert.deepEqual(await stack.storeRequests(), []);
      });
  });
