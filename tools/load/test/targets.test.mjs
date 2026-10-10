import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, test } from 'node:test';
import { contextOf } from '../lib/context.mjs';
import {
  catalogReadAt,
  legacyUrlAt,
  pinnedPageAt,
  probePageAt,
  searchPageAt,
  targetAt,
  warmUpAt,
} from '../lib/targets.mjs';
import { LISTS, META } from './fixtures.mjs';

const context = contextOf(META, LISTS);
const LEGACY_CONF =
  new URL('../../../docker/nginx/server.d/legacy-redirects.conf', import.meta.url);
// The former site's location of nginx: the paths it sends to /api/legacy.
const LEGACY_LOCATION = new RegExp(
  /location ~ (\^\/\(\?:[^ ]+\)\(\?:\/\|\$\)) \{/.exec(readFileSync(LEGACY_CONF, 'utf8'))[1],
);

function walk(target, count) {
  return Array.from({ length: count }, (_, index) => target(context, index));
}

function segmentsOf(url) {
  return new URL(url, 'http://bench').pathname.split('/').slice(1);
}

describe('context', () => {
  test('pins every version but the latest, and reads the latest entries', () => {
    assert.deepEqual(context.archived, ['16.21.1', '16.19.1', '15.13.1', '9.3.1', '0.151.2']);
    assert.deepEqual(context.locales, ['en', 'fr', 'zh-hans']);
    assert.ok(context.pages.includes('runes/8000-precision'), 'a rune page is a path');
    assert.ok(context.reads.includes('items/771036'));
    assert.ok(context.legacyNames.includes('rune/Domination'));
    assert.ok(context.legacyNames.includes('object/1036'));
  });

  test('refuses a stack that has promoted no version', () => {
    assert.throws(() => contextOf({ ...META, latest: null }, LISTS), /promoted/);
  });
});

describe('scenario targets', () => {
  test('S1 asks each archived page once, in every locale, never the latest', () => {
    const size = context.archived.length * context.locales.length *
      (context.lists.length + context.pages.length);
    const urls = walk(pinnedPageAt, size);
    assert.equal(new Set(urls).size, size);
    for (const url of urls) {
      const [locale, version] = segmentsOf(url);
      assert.ok(context.locales.includes(locale), url);
      assert.ok(context.archived.includes(version), url);
    }
  });

  test('S2 searches a current list for something new each time', () => {
    const urls = walk(searchPageAt, 200);
    assert.equal(new Set(urls).size, urls.length);
    for (const url of urls) {
      assert.match(url, /^\/en\/(?:champions|items|runes|summoners)\?q=[a-z]{6}$/);
    }
  });

  test('S3 sweeps the API over archived patches and Data Dragon languages', () => {
    const urls = walk(catalogReadAt, 100);
    assert.equal(new Set(urls).size, urls.length);
    for (const url of urls) {
      const [api, catalog, version, language] = segmentsOf(url);
      assert.deepEqual([api, catalog], ['api', 'catalog']);
      assert.ok(context.archived.includes(version) && context.languages.includes(language), url);
    }
  });

  test("S4 asks every shape of the former site, all routed to nginx's legacy location", () => {
    const urls = walk(legacyUrlAt, 60);
    for (const url of urls) {
      assert.match(new URL(url, 'http://bench').pathname, LEGACY_LOCATION, url);
    }
    assert.ok(urls.some((url) => /^\/\d+(?:\.\d+)+\/champion\/Gone\d+$/.test(url)));
    assert.ok(urls.some((url) => /^\/\d+(?:\.\d+)+\/(?:champions|objects|runes|summoners)$/
      .test(url)));
    assert.ok(urls.some((url) => /\?version=[\d.]+&lang=[a-z]{2}_[A-Z]{2}$/.test(url)));
  });

  test('S5 opens the loader of an archived patch for every list', () => {
    const urls = walk(warmUpAt, 20);
    assert.equal(new Set(urls).size, urls.length);
    for (const url of urls) {
      assert.match(url, /^\/api\/catalog\/[\d.]+\/[a-z]{2}_[A-Z]{2}\/warm-up\?/);
      const lists = new URL(url, 'http://bench').searchParams.getAll('resources');
      assert.deepEqual(lists, ['champions', 'items', 'runes', 'summoners']);
    }
  });

  test('picks the target of a scenario by its name, and refuses an unknown one', () => {
    assert.equal(targetAt('S1', context, 7), pinnedPageAt(context, 7));
    assert.equal(targetAt('S4', context, 7), legacyUrlAt(context, 7));
    assert.throws(() => targetAt('S9', context, 0), /Unknown scenario/);
  });
});

describe('human probe', () => {
  test('alternates a cached list and an entry of the latest patch', () => {
    const urls = Array.from({ length: 40 }, (_, index) => probePageAt(context, index, 0));
    const hot = urls.filter((_, index) => index % 2 === 0);
    assert.ok(hot.every((url) => /^\/(?:en|fr)\/(?:champions|items|runes|summoners)$/.test(url)));
    assert.equal(new Set(hot).size, 8);
    const cold = urls.filter((_, index) => index % 2 === 1);
    for (const url of cold) {
      const [locale, ...page] = segmentsOf(url);
      assert.ok(context.locales.includes(locale), url);
      assert.ok(context.pages.includes(page.join('/')), url);
    }
  });

  test("keeps each run's cold pages apart from the others'", () => {
    const coldOf = (block) => Array.from({ length: 6 }, (_, turn) =>
      probePageAt(context, 2 * turn + 1, block));
    assert.equal(new Set([...coldOf(0), ...coldOf(1)]).size, 12);
  });
});
