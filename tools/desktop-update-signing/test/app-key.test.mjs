import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { appPublicKey } from '../lib/app-key.mjs';
import { readPublicKey } from '../lib/feed-signature.mjs';

const KEY = `${'A'.repeat(43)}=`;

describe('app key', () => {
  it('reads the Ed25519 key compiled into the app', () => {
    assert.equal(readPublicKey(appPublicKey()).asymmetricKeyType, 'ed25519');
  });

  it('reads the constant of UpdateFeeds', () => {
    const source = `public const string FeedPublicKey = "${KEY}";`;

    assert.equal(appPublicKey(source), KEY);
  });

  it('refuses a source without exactly one key', () => {
    const declaration = `FeedPublicKey = "${KEY}";`;

    assert.throws(() => appPublicKey('class UpdateFeeds {}'), /found 0/);
    assert.throws(() => appPublicKey(`${declaration}\n${declaration}`), /found 2/);
  });
});
