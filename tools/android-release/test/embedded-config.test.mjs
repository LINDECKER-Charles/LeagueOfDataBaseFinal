import assert from 'node:assert/strict';
import { generateKeyPairSync } from 'node:crypto';
import { describe, it } from 'node:test';
import { liveUpdateSettingsOf } from '../lib/embedded-config.mjs';

const pem = (key, type) => key.export({ type, format: 'pem' });
const pair = generateKeyPairSync('rsa', { modulusLength: 2048 });
const PUBLIC_KEY = pem(pair.publicKey, 'spki');
const READY_TIMEOUT = 20_000;

function config(liveUpdate) {
  return { appId: 'com.leagueofdatabase.app', plugins: { LiveUpdate: liveUpdate } };
}

describe('liveUpdateSettingsOf', () => {
  it('gives the ready timeout of an app that checks the release key', () => {
    const embedded = config({ publicKey: PUBLIC_KEY, readyTimeout: READY_TIMEOUT });

    assert.deepEqual(liveUpdateSettingsOf(embedded, PUBLIC_KEY), { readyTimeout: READY_TIMEOUT });
  });

  it('accepts the key on one line, as the plugin strips the frame and the line breaks', () => {
    const oneLine = PUBLIC_KEY.replaceAll('\n', '');

    assert.deepEqual(
      liveUpdateSettingsOf(config({ publicKey: oneLine, readyTimeout: READY_TIMEOUT }), PUBLIC_KEY),
      { readyTimeout: READY_TIMEOUT },
    );
  });

  it('stops an app that would take unsigned bundles', () => {
    assert.throws(() => liveUpdateSettingsOf({ plugins: {} }, PUBLIC_KEY), /unsigned/);
  });

  it('stops an app that trusts another key', () => {
    const other = pem(generateKeyPairSync('rsa', { modulusLength: 2048 }).publicKey, 'spki');

    assert.throws(
      () =>
        liveUpdateSettingsOf(config({ publicKey: other, readyTimeout: READY_TIMEOUT }), PUBLIC_KEY),
      /not the public key/,
    );
  });

  it('stops an app that never rolls a faulty bundle back', () => {
    for (const readyTimeout of [undefined, 0, -1, '20000']) {
      assert.throws(
        () => liveUpdateSettingsOf(config({ publicKey: PUBLIC_KEY, readyTimeout }), PUBLIC_KEY),
        /readyTimeout/,
      );
    }
  });

  it('stops an app that embeds the private key', () => {
    const embedded = config({
      publicKey: pem(pair.privateKey, 'pkcs8'),
      readyTimeout: READY_TIMEOUT,
    });

    assert.throws(() => liveUpdateSettingsOf(embedded, PUBLIC_KEY), /private key/);
  });
});
