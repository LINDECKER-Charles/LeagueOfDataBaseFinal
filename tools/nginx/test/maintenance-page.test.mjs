// Runs the maintenance page's scripts (docker/nginx/maintenance/assets) as a browser would, for
// visitors of every kind: the identity and the language they chose on the site, the estimate the
// host gives (or the usual duration), and the reload once the deploy is over. No Docker needed.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, test } from 'node:test';
import { openPage } from '../lib/maintenance-page.mjs';

const WEB = new URL('../../../src/LoDb.Web/src/app/', import.meta.url);
const read = (path) => readFileSync(new URL(path, WEB), 'utf8');
// The identities and their browser colours, as the site declares them.
const THEMES = [...read('core/theme/themes.ts').matchAll(/'([a-z-]+)'/g)].map((m) => m[1]);
const IDENTITIES = read('core/theme/theme-identities.ts');
const browserColorOf = (theme) =>
  new RegExp(`'?${theme}'?: \\{[^}]*?browserColor: '(#[0-9a-f]{6})'`).exec(IDENTITIES)[1];
const MINUTE_MS = 60_000;
const NOW = Date.UTC(2026, 9, 12, 10, 0, 0);
const NOW_SECONDS = NOW / 1000;
const httpDate = (ms) => new Date(ms).toUTCString();
// The host's state, as tools/deploy/maintenance.sh writes it.
const measured = (startedMinutesAgo, expectedMinutes) => ({
  status: 200,
  body: {
    startedAt: NOW_SECONDS - startedMinutesAgo * 60,
    expectedSeconds: expectedMinutes * 60,
  },
  date: httpDate(NOW),
});
const unmeasured = { status: 200, body: { startedAt: NOW_SECONDS }, date: httpDate(NOW) };
const over = { status: 404 };

async function loaded(options) {
  const page = openPage({ clientNow: NOW, status: () => unmeasured, ...options });
  await page.load();
  return page;
}

describe('maintenance page', () => {
  test('paints the identity the visitor chose, the default one otherwise', () => {
    assert.ok(THEMES.length === 4, THEMES.join());
    for (const theme of THEMES) {
      const page = openPage({ cookie: `a=1; lod_theme=${theme}; b=2`, status: () => unmeasured });
      assert.equal(page.root.getAttribute('data-theme'), theme);
      assert.equal(page.meta.getAttribute('content'), browserColorOf(theme), theme);
    }
    for (const cookie of ['', 'lod_theme=bilgewater', 'lod_theme=']) {
      const page = openPage({ cookie, status: () => unmeasured });
      assert.equal(page.root.getAttribute('data-theme'), 'hextech', cookie);
    }
  });

  test("speaks the page's own locale first, whatever the visitor's choice", () => {
    const page = openPage({
      path: '/de/champions/Ahri',
      cookie: 'lod_prefs=loc=fr&l=fr_FR&v=16.1.1',
      languages: ['ja-JP'],
      status: () => unmeasured,
    });
    assert.equal(page.root.getAttribute('lang'), 'de');
    assert.equal(page.document.title, 'Wartungsarbeiten – League of Data Base');
  });

  test('speaks the locale the visitor asked the switcher to remember, off a locale', () => {
    for (const path of ['/', '/b/Zx81kq', '/admin']) {
      const page = openPage({
        path,
        cookie: 'lod_prefs=loc=zh-hant&v=16.1.1',
        languages: ['en-US'],
        status: () => unmeasured,
      });
      assert.equal(page.root.getAttribute('lang'), 'zh-hant', path);
    }
  });

  test("otherwise speaks the browser's first language the site has, as / negotiates", () => {
    const cases = [
      [['pt-BR', 'en'], 'pt'],
      [['xx', 'de-AT'], 'de'],
      [['zh-TW'], 'zh-hant'],
      [['zh-Hans-HK'], 'zh-hans'],
      [['zh'], 'zh-hans'],
      [['xx-YY'], 'en'],
      [[], 'en'],
    ];
    for (const [languages, locale] of cases) {
      const page = openPage({
        path: '/',
        cookie: 'lod_prefs=loc=//evil',
        languages,
        status: () => unmeasured,
      });
      assert.equal(page.root.getAttribute('lang'), locale, languages.join());
    }
  });

  test('writes right to left in Arabic, left to right elsewhere', () => {
    assert.equal(
      openPage({ path: '/ar/', status: () => unmeasured }).root.getAttribute('dir'),
      'rtl',
    );
    assert.equal(
      openPage({ path: '/fr/', status: () => unmeasured }).root.getAttribute('dir'),
      'ltr',
    );
  });

  test("writes the page's texts in the visitor's language", async () => {
    const page = await loaded({ path: '/fr/items' });
    assert.deepEqual(page.texts(), {
      heading: 'Maintenance en cours',
      lead: 'Une nouvelle version du site est en cours de déploiement. Il revient très vite.',
      autoReload: 'Cette page se rechargera d’elle-même dès le retour du site.',
      retry: 'Réessayer',
    });
  });

  test('shows the usual duration until the host has measured a deploy', async () => {
    for (const status of [
      () => unmeasured,
      () => ({ status: 502 }),
      () => {
        throw new TypeError('Failed to fetch');
      },
    ]) {
      const page = await loaded({ path: '/en/', status });
      assert.equal(page.label.textContent, 'Usual duration');
      assert.equal(page.value.textContent, '10–15 minutes');
      assert.equal(page.value.getAttribute('data-estimate-kind'), 'typical');
    }
  });

  test("counts down the previous deploy's duration, in whole minutes", async () => {
    const page = await loaded({ path: '/en/', status: () => measured(2, 4.5) });
    assert.equal(page.label.textContent, 'Estimated time remaining');
    assert.equal(page.value.textContent, '3 minutes');
    assert.equal(page.value.getAttribute('data-estimate-kind'), 'remaining');
    const russian = await loaded({ path: '/ru/', status: () => measured(0, 0.5) });
    assert.equal(russian.value.textContent, '1 минута');
  });

  test("reads the time on the host's clock, not on a visitor's that is off", async () => {
    const page = await loaded({
      path: '/en/',
      clientNow: NOW + 30 * MINUTE_MS,
      status: () => measured(1, 6),
    });
    assert.equal(page.value.textContent, '5 minutes');
  });

  test('says it is almost done once the estimate has passed', async () => {
    const page = await loaded({ path: '/en/', status: () => measured(8, 5) });
    assert.equal(page.label.textContent, 'Estimated time remaining');
    assert.equal(page.value.textContent, 'Almost there, just a few more moments');
    assert.equal(page.value.getAttribute('data-estimate-kind'), 'overdue');
  });

  test('keeps the last estimate while nginx cannot be reached', async () => {
    const answers = [measured(1, 5), { status: 502 }];
    const page = await loaded({ path: '/en/', status: () => answers.shift() });
    await page.tick();
    assert.equal(page.value.textContent, '4 minutes');
    assert.deepEqual(page.reloads, []);
  });

  test('reloads the page asked for once the deploy is over, and only then', async () => {
    const answers = [measured(1, 5), over];
    const page = await loaded({ path: '/en/champions', status: () => answers.shift() });
    assert.deepEqual(page.reloads, []);
    await page.tick();
    assert.deepEqual(page.reloads, ['/en/champions']);
  });
});
