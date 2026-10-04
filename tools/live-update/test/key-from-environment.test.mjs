import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { PRIVATE_KEY_VARIABLE, keyFromEnvironment } from '../lib/key-from-environment.mjs';
import { testKeyPair } from './test-keys.mjs';

const { privateKey } = testKeyPair();

describe('keyFromEnvironment', () => {
  it('reads the PEM of the variable', () => {
    const environment = { [PRIVATE_KEY_VARIABLE]: `\n${privateKey}` };

    assert.equal(keyFromEnvironment(PRIVATE_KEY_VARIABLE, environment), privateKey.trim());
  });

  it('reads it base64 encoded, as some secret stores keep it', () => {
    const environment = { [PRIVATE_KEY_VARIABLE]: Buffer.from(privateKey).toString('base64') };

    assert.equal(keyFromEnvironment(PRIVATE_KEY_VARIABLE, environment), privateKey.trim());
  });

  it('asks for the variable when it is missing or blank', () => {
    assert.throws(() => keyFromEnvironment(PRIVATE_KEY_VARIABLE, {}), /is not set/);
    assert.throws(
      () => keyFromEnvironment(PRIVATE_KEY_VARIABLE, { [PRIVATE_KEY_VARIABLE]: ' ' }),
      /is not set/,
    );
  });

  it('refuses a path or any other value instead of the key', () => {
    const environment = { [PRIVATE_KEY_VARIABLE]: '/secure/lodb-live-update.pem' };

    assert.throws(() => keyFromEnvironment(PRIVATE_KEY_VARIABLE, environment), /neither a PEM/);
  });
});
