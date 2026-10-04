import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  blockName,
  diffBlocks,
  normalizeJsonLd,
  typesOf,
  urlRewriter,
} from '../lib/json-ld.mjs';
import { legacyKey, nextKey } from '../lib/page-key.mjs';

const PROD = 'https://league-of-data-base.com';
const prodRewrite = urlRewriter([PROD], (path) => legacyKey(path));
const nextRewrite = urlRewriter(['http://localhost'], (path) => nextKey(path).key);

test('rewrites the URLs of each site into the same page keys', () => {
  const annie = 'detail:champions:Annie@latest#game';

  assert.equal(prodRewrite(`${PROD}/champion/Annie#game`), annie);
  assert.equal(nextRewrite('http://localhost/en/champions/Annie#game'), annie);
  assert.equal(prodRewrite(`${PROD}/#website`), 'home#website');
  assert.equal(nextRewrite('http://localhost/#website'), 'home#website');
  assert.equal(nextRewrite('http://localhost/cdn/blobs/ab12.png'), 'asset:/cdn/blobs/ab12.png');
  assert.equal(prodRewrite('https://github.com/lodb'), 'https://github.com/lodb');
  assert.equal(prodRewrite('League Of Data Base'), 'League Of Data Base');
});

test('lists every type once, nested ones included', () => {
  const blocks = [{ '@graph': [{ '@type': 'WebSite' }, { '@type': 'WebPage' }] },
    { '@type': 'VideoGame', character: { '@type': 'Person', additionalProperty: [
      { '@type': 'PropertyValue' }, { '@type': 'PropertyValue' }] } }];

  const types = ['Person', 'PropertyValue', 'VideoGame', 'WebPage', 'WebSite'];

  assert.deepEqual(typesOf(blocks), types);
  assert.equal(blockName(blocks[0]), '@graph');
  assert.equal(blockName(blocks[1]), 'VideoGame');
});

test('reports each leaf that differs or exists on one side only', () => {
  const prod = normalizeJsonLd(
    [{ '@type': 'ItemList', numberOfItems: 2, itemListElement: [
      { name: 'Aatrox', url: `${PROD}/champion/Aatrox` }, { name: 'Ahri' }] }],
    prodRewrite,
  );
  const next = normalizeJsonLd(
    [{ '@type': 'ItemList', numberOfItems: 1, itemListElement: [
      { name: 'Aatrox', url: 'http://localhost/fr/champions/Aatrox' }] }],
    nextRewrite,
  );

  assert.deepEqual(diffBlocks(prod, next), [
    { path: 'ItemList.itemListElement[1].name', prod: 'Ahri', next: undefined },
    { path: 'ItemList.numberOfItems', prod: 2, next: 1 },
  ]);
});
