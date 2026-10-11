import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { appPublicKeys, keyList, parseKeyList } from '../lib/app-key.mjs';
import { readPublicKey } from '../lib/feed-signature.mjs';

const KEY = `${'A'.repeat(43)}=`;
const NEXT = `${'B'.repeat(43)}=`;
const list = (...keys) =>
  `public static readonly ImmutableArray<string> FeedPublicKeys =\n[\n${keys
    .map((key) => `    "${key}",\n`)
    .join('')}];`;

describe('app keys', () => {
  it('reads the Ed25519 keys compiled into the app', () => {
    const keys = appPublicKeys();

    assert.ok(keys.length >= 1);
    for (const key of keys) {
      assert.equal(readPublicKey(key).asymmetricKeyType, 'ed25519');
    }
  });

  it('reads the list of UpdateFeeds, in its order', () => {
    assert.deepEqual(appPublicKeys(list(KEY)), [KEY]);
    assert.deepEqual(appPublicKeys(list(KEY, NEXT)), [KEY, NEXT]);
  });

  it('refuses a source without exactly one list', () => {
    assert.throws(() => appPublicKeys('class UpdateFeeds {}'), /found 0/);
    assert.throws(() => appPublicKeys(`${list(KEY)}\n${list(KEY)}`), /found 2/);
  });

  it('refuses a list that is empty or holds anything but keys', () => {
    assert.throws(() => appPublicKeys(list()), /Ed25519 keys in base64 only/);
    assert.throws(() => appPublicKeys(list(KEY, 'not a key')), /Ed25519 keys in base64 only/);
  });

  it('writes and reads the keys as the smoke report lists them', () => {
    assert.equal(keyList([KEY, NEXT]), `${KEY} ${NEXT}`);
    assert.deepEqual(parseKeyList(` ${KEY}  ${NEXT}\n`), [KEY, NEXT]);
  });
});
