// Boots the built nginx image in front of a page store it cannot use (ADR 0011): one whose
// certificate no authority signed, one whose name does not resolve. Either way nginx starts and
// renders the pages with the SSR server, never asking the store. Needs Docker and the image's
// tag in LODB_NGINX_IMAGE (tools/nginx/README.md); skipped without it.
import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
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
// A page the stub store holds, and what the stub's SSR server answers for it.
const STORED = '/en/15.2.1/champions';
const RENDERED = 'archive\n';
const CLIENT = { forwardedFor: '203.0.113.1' };

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
          const answer = await send(stack.port, STORED, CLIENT);
          assert.equal(answer.body.toString(), RENDERED);
          assert.equal(answer.headers['x-lodb-source'], 'ssr');
          assert.deepEqual(await stack.storeRequests(), []);
        } finally {
          await stack.stop();
        }
      });
    }
  });
