import assert from 'node:assert/strict';
import { test } from 'node:test';
import { expectationOf, localePattern, locationMatches } from '../lib/legacy-urls.mjs';

const BASE = 'http://localhost:18280';

function accepts(path, location) {
  return locationMatches(expectationOf(path), location, BASE);
}

test('reads the locale from the former ?lang=, English by default', () => {
  assert.equal(localePattern(undefined), 'en');
  assert.equal(localePattern('en_GB'), 'en');
  assert.equal(localePattern('garbage'), 'en');
  assert.equal(localePattern('fr_FR'), '(?:fr(?:-[a-z]+)?|en)');
});

test('moves the catalogue lists and details below the locale', () => {
  assert.equal(accepts('/champions', '/en/champions'), true);
  assert.equal(accepts('/objects?lang=fr_FR', '/fr/items'), true);
  assert.equal(accepts('/summoners?lang=zh_CN', '/zh-hans/summoners'), true);
  assert.equal(accepts('/champion/Ahri', '/en/champions/Ahri'), true);
  assert.equal(accepts('/object/1004', '/en/items/1004-faerie-charm'), true);
  assert.equal(accepts('/rune/Domination', '/en/runes/8100-domination'), true);
  assert.equal(accepts('/summoner/SummonerFlash', '/en/summoners/SummonerFlash'), true);
});

test('keeps a pinned version, or drops it for the latest one', () => {
  assert.equal(accepts('/14.1.1/champions', '/en/14.1.1/champions'), true);
  assert.equal(accepts('/16.19.1/runes', '/en/runes'), true);
  assert.equal(accepts('/14.1.1/object/1004', '/en/14.1.1/items/1004-faerie-charm'), true);
  assert.equal(accepts('/objects?version=14.1.1&lang=fr_FR', '/fr/14.1.1/items'), true);
  assert.equal(accepts('/14.1.1/champions', '/en/13.1.1/champions'), false);
});

test('refuses a Location of another shape, origin or query', () => {
  assert.equal(accepts('/champion/Ahri', '/en/items/Ahri'), false);
  assert.equal(accepts('/object/1004', '/en/items/Faerie'), false);
  assert.equal(accepts('/champions', '/fr/champions'), false);
  assert.equal(accepts('/champions', 'https://elsewhere.example/en/champions'), false);
  assert.equal(accepts('/champions', '/en/champions?version=1.2.3'), false);
  assert.equal(accepts('/champions?lang=en_GB', '/en/champions?lang=en_GB'), true);
  assert.equal(accepts('/champions', `${BASE}/en/champions`), true);
});

test('moves the pages and the account pages below the locale', () => {
  assert.equal(accepts('/home', '/en/'), true);
  assert.equal(accepts('/home?lang=fr_FR', '/fr/'), true);
  assert.equal(accepts('/about/data', '/en/about/data'), true);
  assert.equal(accepts('/legal/cookies', '/en/legal/cookies'), true);
  assert.equal(accepts('/u/Faker', '/en/u/Faker'), true);
  assert.equal(accepts('/login', '/en/account/login'), true);
  assert.equal(accepts('/reset-password', '/en/account/forgot-password'), true);
  assert.equal(accepts('/builds/42/edit', '/en/account/builds/42/edit'), true);
  assert.equal(accepts('/about', '/en/faq'), false);
});

test('moves the old sitemaps onto the new index', () => {
  assert.equal(accepts('/sitemaps/latest.xml', '/sitemap.xml'), true);
  assert.equal(accepts('/sitemaps/9.1.1.xml', '/sitemaps/en/9.1.1.xml'), true);
  assert.equal(accepts('/sitemaps/9.1.1.xml', '/sitemaps/en/latest.xml'), true);
  assert.equal(expectationOf('/sitemap.xml').kind, 'sitemap');
});

test('sorts out the root, the contracts and what the old site never served', () => {
  assert.equal(expectationOf('/').kind, 'root');
  assert.equal(accepts('/', '/fr/'), true);
  for (const path of ['/b/abc', '/v1/usage', '/webhooks/stripe', '/cdn/blobs/x.png']) {
    assert.equal(expectationOf(path).kind, 'contract', path);
  }
  for (const path of ['/nowhere', '/14.1.1/about', '/champion', '/champions/Ahri/extra']) {
    assert.equal(expectationOf(path).kind, 'unknown', path);
  }
});
