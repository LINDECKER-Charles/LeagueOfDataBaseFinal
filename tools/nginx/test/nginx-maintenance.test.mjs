// Boots the built nginx image as a host runs it (the edge's secret set) and turns the maintenance
// switch as the deploy job does (tools/deploy/maintenance.sh, server.d/maintenance.conf): every
// page answers the maintenance page with a 503, everything else keeps its own answer, the page's
// files and the deploy's state stay served, and the site comes back once the switch is gone.
// Needs Docker and the image's tag in LODB_NGINX_IMAGE (tools/nginx/README.md); skipped without.
import assert from 'node:assert/strict';
import { after, before, describe, test } from 'node:test';
import { send } from '../lib/client.mjs';
import { startStack } from '../lib/stack.mjs';

const IMAGE = process.env.LODB_NGINX_IMAGE;
// What the deploy job generates on a host: 32 random bytes in hexadecimal.
const EDGE_SECRET = '5e'.repeat(32);
const HOST = { LODB_EDGE_CIDR: '0.0.0.0/0', LODB_EDGE_SECRET: EDGE_SECRET };
// The folder compose.deploy.yaml mounts, a tmpfs here (lib/stack.mjs).
const SWITCH = '/srv/maintenance/active.json';
const STATE = '{"startedAt":1760263200,"expectedSeconds":74}';
// What the stub's SSR server answers for a page (lib/upstream-stub.conf).
const STUB_PAGE = 'page\n';
// Every shape of page the SSR server's pre-router accepts: `/`, a locale, a past patch, a shared
// build, the admin, a page rendered in the browser.
const PAGES = [
  '/',
  '/en/',
  '/fr/champions/Ahri',
  '/zh-hant/15.1.1/items',
  '/b/Zx81kq',
  '/admin',
  '/en/account/login',
];
// Paths that are not pages, each with what it answers during a deploy: the stub's API, its
// bundles, nginx's probe, a crawler file, the former site's 301, a first segment that only
// starts like a locale (the SSR server's own 404, a page of the stub here).
const NOT_PAGES = {
  '/api/meta': 200,
  '/v1/champions': 200,
  '/webhooks/stripe': 200,
  '/build/main-ABCD1234.js': 200,
  '/healthz': 200,
  '/robots.txt': 200,
  '/champions': 301,
  '/english': 200,
};
const PAGE_POLICY =
  "default-src 'none'; script-src 'self'; style-src 'self'; font-src 'self';" +
  " img-src 'self'; connect-src 'self'; base-uri 'none'; form-action 'none';" +
  " frame-ancestors 'none'; require-trusted-types-for 'script'";
const FILES = {
  '/maintenance/maintenance.js': 'application/javascript',
  '/maintenance/messages.js': 'application/javascript',
  '/maintenance/page.css': 'text/css',
  '/maintenance/fonts/BeaufortforLOL-Bold.woff2': 'font/woff2',
  '/maintenance/fonts/grenze/latin.woff2': 'font/woff2',
  '/maintenance/favicon-96x96.png': 'image/png',
};

describe(
  'nginx image during a deploy',
  { skip: IMAGE ? false : 'LODB_NGINX_IMAGE is not set' },
  () => {
    let stack;
    const get = (path, options) => send(stack.port, path, { edge: EDGE_SECRET, ...options });
    const turnOn = () => stack.exec('sh', '-c', `printf '%s' '${STATE}' > ${SWITCH}`);
    const turnOff = () => stack.exec('rm', '-f', SWITCH);

    before(async () => {
      stack = await startStack(IMAGE, HOST);
    });

    after(() => stack?.stop());

    test('serves its pages, and no deploy state, while the switch is off', async () => {
      const page = await get('/fr/champions/Ahri');
      assert.equal(page.status, 200);
      assert.equal(page.body.toString(), STUB_PAGE);
      assert.equal((await get('/maintenance/status.json')).status, 404);
    });

    test('answers every page with the maintenance page, never kept', async () => {
      await turnOn();
      for (const path of PAGES) {
        const { status, headers, body } = await get(path);
        assert.equal(status, 503, path);
        assert.equal(headers['content-type'], 'text/html; charset=utf-8', path);
        assert.match(body.toString(), /data-message="heading"/, path);
        assert.equal(headers['retry-after'], '120', path);
        assert.equal(headers['cache-control'], 'no-store', path);
        assert.equal(headers['content-security-policy'], PAGE_POLICY, path);
        assert.equal(headers['x-frame-options'], 'DENY', path);
        assert.equal(headers['x-cache-status'], undefined, path);
      }
    });

    test('leaves what is not a page to its own answer', async () => {
      await turnOn();
      for (const [path, expected] of Object.entries(NOT_PAGES)) {
        const { status, body } = await get(path);
        assert.equal(status, expected, path);
        assert.doesNotMatch(body.toString(), /data-message/, path);
      }
    });

    test("answers the deploy's state to the page, never kept", async () => {
      await turnOn();
      const { status, headers, body } = await get('/maintenance/status.json');
      assert.equal(status, 200);
      assert.equal(headers['content-type'], 'application/json');
      assert.equal(headers['cache-control'], 'no-store');
      assert.deepEqual(JSON.parse(body.toString()), JSON.parse(STATE));
    });

    test("serves the page's files under a sandbox policy, but never the page itself", async () => {
      for (const [path, type] of Object.entries(FILES)) {
        const { status, headers } = await get(path);
        assert.equal(status, 200, path);
        assert.equal(headers['content-type'].split(';')[0], type, path);
        assert.equal(headers['content-security-policy'], "default-src 'none'; sandbox", path);
        assert.equal(headers['cache-control'], 'no-cache', path);
      }
      for (const path of ['/maintenance/index.html', '/maintenance/fonts/../index.html']) {
        assert.equal((await get(path)).status, 404, path);
      }
    });

    test('still refuses a request the edge did not send', async () => {
      await turnOn();
      assert.equal((await get('/en/', { edge: undefined })).status, 403);
    });

    test('serves the pages again once the switch is gone', async () => {
      await turnOn();
      await turnOff();
      const page = await get('/en/');
      assert.equal(page.status, 200);
      assert.equal(page.body.toString(), STUB_PAGE);
      assert.equal((await get('/maintenance/status.json')).status, 404);
    });
  },
);
