// Boots the built nginx image in front of the stub's page store (lib/upstream-stub.conf), as
// nginx-pages-store.test.mjs does, and checks how long it keeps a past patch's page by the
// class of its object: the open state's page (revisable) gets that class and leaves the hot
// cache as soon as its object says, while a bundle is kept a year whatever its object says; a
// patch page comes from the store as any other page. Needs Docker and the image's tag in
// LODB_NGINX_IMAGE (tools/nginx/README.md); skipped without it.
import assert from 'node:assert/strict';
import { after, before, describe, test } from 'node:test';
import { send } from '../lib/client.mjs';
import { startStack } from '../lib/stack.mjs';

const IMAGE = process.env.LODB_NGINX_IMAGE;
// The stub's store as nginx-pages-store.test.mjs uses it: bucket lodb-pages, its pages on
// epoch 5, over plain HTTP on its alias, each client a burst of four pages a minute.
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
const OBJECTS = '/lodb-pages/pages/5';
// The patch page of a past patch, which the pass stores beside its lists and entities.
const PATCH_PAGE = '/fr/15.2.1/changes';
// A page of the open state, stored with the revisable class's lifetime; one whose object lives
// a second, for the hot cache's expiry, and a bundle whose object says as much; and the
// classes nginx gives the client (core/routing/response/cache-control.ts).
const REVISABLE = '/en/14.1.1/items/1036-long-sword';
const BRIEF = '/en/14.2.1/champions';
const BRIEF_BUNDLE = '/build/brief-ABCD1234.js';
const CLASSES = {
  archived: 'public, max-age=3600, s-maxage=31536000',
  revisable: 'public, max-age=3600, s-maxage=3600, stale-while-revalidate=3600',
};
// Past the brief object's second: nginx counts its lifetime in whole seconds.
const BRIEF_LIFETIME_MS = 2_500;

// A client address of its own for each use: the burst of one never runs into another's.
let lastClient = 0;
const aClient = () => ({ forwardedFor: `203.0.113.${(lastClient += 1)}` });
// What a store adds of its own (snippets/pages-store/answer.conf), as
// nginx-pages-store.test.mjs names it.
const STORE_HEADER = /^(?:x-amzn?-|access-control-|etag$|last-modified$|accept-ranges$)/;
const storeHeadersOf = (answer) => Object.keys(answer.headers).filter((name) =>
  STORE_HEADER.test(name));
const sourceOf = (answer) =>
  [answer.headers['x-cache-status'], answer.headers['x-lodb-source']];

describe('nginx image keeping the store\'s pages by their class',
  { skip: IMAGE ? false : 'LODB_NGINX_IMAGE is not set' }, () => {
    let stack;
    const get = (path, options) => send(stack.port, path, options);

    before(async () => {
      stack = await startStack(IMAGE, STORE);
    });

    after(() => stack?.stop());

    test('serves a revisable stored page with its class, kept as long as its object says',
      async () => {
        const visitor = aClient();
        const answers = [await get(REVISABLE, visitor), await get(REVISABLE, visitor)];

        assert.deepEqual(answers.map(sourceOf), [['MISS', 'store'], ['HIT', 'cache']]);
        for (const answer of answers) {
          assert.equal(answer.status, 200);
          assert.equal(answer.body.toString(), stack.storedPage);
          assert.equal(answer.headers['cache-control'], CLASSES.revisable);
          assert.deepEqual(storeHeadersOf(answer), []);
        }
      });

    test('reads the store again once a stored page outlived its object, never a bundle',
      async () => {
        const before = await stack.storeRequests();
        const fetched = [await get(BRIEF, aClient()), await get(BRIEF_BUNDLE, aClient())];
        await new Promise((resolve) => setTimeout(resolve, BRIEF_LIFETIME_MS));
        const page = await get(BRIEF, aClient());
        const bundle = await get(BRIEF_BUNDLE, aClient());

        assert.deepEqual(sourceOf(fetched[0]), ['MISS', 'store']);
        assert.deepEqual(sourceOf(page), ['EXPIRED', 'store']);
        assert.equal(page.body.toString(), stack.storedPage);
        // Not the revisable class's value: the class an object of no known class gets.
        assert.equal(page.headers['cache-control'], CLASSES.archived);
        assert.deepEqual([fetched[1], bundle].map((answer) => answer.headers['x-cache-status']),
          ['MISS', 'HIT']);
        assert.equal(bundle.body.toString(), 'brief bundle\n');
        assert.deepEqual((await stack.storeRequests()).slice(before.length),
          [`GET ${OBJECTS}${BRIEF}`, `GET /lodb-pages${BRIEF_BUNDLE}`, `GET ${OBJECTS}${BRIEF}`]);
      });

    test('serves a past patch\'s patch page from the store', async () => {
      const visitor = aClient();
      const before = await stack.storeRequests();
      const answers = [await get(PATCH_PAGE, visitor), await get(PATCH_PAGE, visitor)];

      assert.deepEqual(answers.map(sourceOf), [['MISS', 'store'], ['HIT', 'cache']]);
      assert.equal(answers[1].body.toString(), stack.storedPage);
      assert.deepEqual((await stack.storeRequests()).slice(before.length),
        [`GET ${OBJECTS}${PATCH_PAGE}`]);
    });
  });
