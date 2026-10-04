import assert from 'node:assert/strict';
import { test } from 'node:test';
import { legacyKey, nextKey } from '../lib/page-key.mjs';

const LATEST = '16.19.1';

test('names a legacy page by kind, id and version', () => {
  assert.equal(legacyKey('https://league-of-data-base.com/'), 'home');
  assert.equal(legacyKey('/home?lang=fr_FR'), 'home');
  assert.equal(legacyKey('/objects'), 'list:items@latest');
  assert.equal(legacyKey('/16.14.1/champions'), 'list:champions@16.14.1');
  assert.equal(legacyKey('/champion/Annie'), 'detail:champions:Annie@latest');
  assert.equal(legacyKey('/object/3031?version=16.14.1'), 'detail:items:3031@16.14.1');
  assert.equal(
    legacyKey('/summoner/SummonerFlash_Jade'),
    'detail:summoners:SummonerFlash_Jade@latest',
  );
  assert.equal(legacyKey('/about/data'), 'page:about/data');
});

test('numbers the legacy rune paths as Riot does', () => {
  assert.equal(legacyKey('/rune/Domination'), 'detail:runes:8100@latest');
  assert.equal(legacyKey('/16.14.1/rune/Inspiration'), 'detail:runes:8300@16.14.1');
});

test('folds an explicit latest version into the short form', () => {
  assert.equal(legacyKey(`/${LATEST}/champion/Annie`, LATEST), 'detail:champions:Annie@latest');
  const { key } = nextKey(`/en/${LATEST}/champions/Annie`, LATEST);

  assert.equal(key, 'detail:champions:Annie@latest');
});

test('names a page of ADR 0005 the same way, its locale apart', () => {
  assert.deepEqual(nextKey('http://localhost/fr/'), { key: 'home', locale: 'fr' });
  assert.deepEqual(nextKey('/zh-hant/items'), { key: 'list:items@latest', locale: 'zh-hant' });
  assert.equal(nextKey('/en/items/3031-infinity-edge').key, 'detail:items:3031@latest');
  assert.equal(nextKey('/ko/16.14.1/runes/8100-domination').key, 'detail:runes:8100@16.14.1');
  assert.equal(nextKey('/en/champions/Annie?lang=en_GB').key, 'detail:champions:Annie@latest');
  assert.equal(nextKey('/en/legal/notice').key, 'page:legal/notice');
});

test('keeps an unknown shape as a page, never a catalogue key', () => {
  assert.equal(nextKey('/en/champions/Annie/skins').key, 'page:champions/Annie/skins');
  assert.equal(legacyKey('/champion/Annie/skins'), 'page:champion/Annie/skins');
});
