// Boots the built nginx image as a local stack runs it, without the edge's secret (compose.yaml
// passes an empty one, and the E2E suite sends no header), and checks what a page answer
// carries: the constant security headers on every location that serves one, `/` and the locale
// a visitor asked to remember, a private page never kept, and the former site's URLs, each
// handed to the API as sent, an unknown one to the SSR server's 404. Needs Docker and the
// image's tag in LODB_NGINX_IMAGE (tools/nginx/README.md); skipped without it.
import assert from 'node:assert/strict';
import { after, before, describe, test } from 'node:test';
import { send } from '../lib/client.mjs';
import { startStack } from '../lib/stack.mjs';

const IMAGE = process.env.LODB_NGINX_IMAGE;
// What the deploy job generates on a host: 32 random bytes in hexadecimal.
const EDGE_SECRET = '5e'.repeat(32);
// snippets/security-headers.conf. `add_header` does not reach a block that declares its own:
// each location that does includes the file again, which these checks hold it to.
const CONSTANT_HEADERS = {
  'strict-transport-security': 'max-age=63072000; includeSubDomains; preload',
  'x-content-type-options': 'nosniff',
  'x-frame-options': 'DENY',
  'referrer-policy': 'strict-origin-when-cross-origin',
  'cross-origin-opener-policy': 'same-origin',
};
const DENIED_FEATURE = 'camera=()';
// The switcher's cookie, written by the browser alone (sites/site.conf, location = /).
const REMEMBERED_FRENCH = 'lod_prefs=loc=fr&v=16.1.1';
const OFF_SHAPE_LOCALE = 'lod_prefs=loc=//evil.example';
const PRIVATE_PAGE = '/en/account/profile';
// One path per alternative of the former site's location (server.d/legacy-redirects.conf).
const LEGACY_PATHS = [
  '/home',
  '/champion/Ahri',
  '/champions',
  '/object/1004?lang=fr_FR',
  '/objects',
  '/rune/Domination',
  '/runes',
  '/summoner/SummonerFlash',
  '/summoners',
  '/about/data',
  '/faq',
  '/changelog',
  '/developers',
  '/donate',
  '/legal/privacy',
  '/trends',
  '/u/Faker',
  '/login',
  '/register',
  '/profile/api',
  '/reset-password',
  '/builds/42/edit',
  '/14.24.1/champions',
];
// The stub's API knows no such name; its SSR server renders the 404 (lib/upstream-stub.conf).
const UNKNOWN_LEGACY = '/champion/NotAChampion';
const NOT_FOUND_PAGE = 'not found\n';
// A first segment the former site never owned, though it starts like one: the SSR server's.
const LEGACY_LOOKALIKE = '/championship';
// The former site's URLs outnumber the image's legacy burst: rates are nginx-image's checks.
const LOCAL = { LODB_EDGE_CIDR: '0.0.0.0/0', LODB_EDGE_SECRET: '', LODB_LEGACY_BURST: '100' };

describe('nginx image as a local stack runs it',
  { skip: IMAGE ? false : 'LODB_NGINX_IMAGE is not set' }, () => {
    let stack;
    const get = (path, options) => send(stack.port, path, options);

    before(async () => {
      stack = await startStack(IMAGE, LOCAL);
    });

    after(() => stack?.stop());

    test('asks no header, whatever a client sends in it', async () => {
      for (const edge of [undefined, EDGE_SECRET, 'anything']) {
        assert.equal((await get('/en/about', { edge })).status, 200, edge);
      }
    });

    test('sends the constant security headers from every location that answers a page',
      async () => {
        const answers = {
          page: await get('/en/about'),
          'past patch': await get('/en/15.1.1/champions'),
          root: await get('/'),
          'remembered root': await get('/', { headers: { cookie: REMEMBERED_FRENCH } }),
          'former URL': await get('/champions'),
          'unknown former URL': await get(UNKNOWN_LEGACY),
        };
        for (const [label, { headers }] of Object.entries(answers)) {
          for (const [name, value] of Object.entries(CONSTANT_HEADERS)) {
            assert.equal(headers[name], value, `${label}: ${name}`);
          }
          assert.ok(headers['permissions-policy'].split(', ').includes(DENIED_FEATURE), label);
        }
      });

    test('answers / from a remembered locale, and never keeps it in a cache', async () => {
      const remembered = await get('/', { headers: { cookie: REMEMBERED_FRENCH } });
      assert.equal(remembered.status, 302);
      assert.equal(remembered.headers.location, '/fr/');
      assert.equal(remembered.headers['cache-control'], 'private, no-store');
      // Without a locale of the right shape, the SSR server negotiates it, every time.
      for (const cookie of [undefined, OFF_SHAPE_LOCALE]) {
        const negotiated = await get('/', { headers: cookie ? { cookie } : {} });
        assert.equal(negotiated.status, 200, cookie);
        assert.equal(negotiated.headers['x-cache-status'], undefined, cookie);
      }
    });

    test('never keeps a page its server marks private', async () => {
      const statuses = [];
      for (let sent = 0; sent < 2; sent += 1) {
        statuses.push((await get(PRIVATE_PAGE)).headers['x-cache-status']);
      }
      assert.deepEqual(statuses, ['MISS', 'MISS']);
    });

    test("hands each former URL to the API as sent, an unknown one to the SSR server's 404",
      async () => {
        for (const path of LEGACY_PATHS) {
          const moved = await get(path);
          assert.equal(moved.status, 301, path);
          assert.equal(moved.headers['x-seen-uri'], `/api/legacy${path}`, path);
        }
        const unknown = await get(UNKNOWN_LEGACY);
        assert.equal(unknown.status, 404);
        assert.equal(unknown.body.toString(), NOT_FOUND_PAGE);
        const lookalike = await get(LEGACY_LOOKALIKE);
        assert.equal(lookalike.status, 200);
        assert.equal(lookalike.headers['x-seen-uri'], undefined);
      });
  });
