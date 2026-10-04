import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  expandSteps, resolveKeys, resolveRequest, validateGroup, validateStep,
} from '../lib/scenarios.mjs';

const request = { method: 'GET', path: '/v1/usage' };

describe('validateStep', () => {
  it('accepts a request and a known action', () => {
    validateStep({ id: 'a', request }, 'g');
    validateStep({ id: 'b', action: 'sleep', ms: 1 }, 'g');
  });

  it('rejects a step that is both, neither, or an unknown action', () => {
    assert.throws(() => validateStep({ id: 'a', request, action: 'sleep' }, 'g'));
    assert.throws(() => validateStep({ id: 'a' }, 'g'));
    assert.throws(() => validateStep({ id: 'a', action: 'reboot' }, 'g'));
  });

  it('rejects ids that would collide with repeated steps', () => {
    assert.throws(() => validateStep({ id: 'a#1', request }, 'g'), /without '#'/);
  });
});

describe('validateGroup', () => {
  it('rejects duplicate ids', () => {
    const group = { name: 'g', steps: [{ id: 'a', request }, { id: 'a', request }] };
    assert.throws(() => validateGroup(group), /duplicate/);
  });
});

describe('expandSteps', () => {
  it('unrolls repeat into numbered steps without the repeat field', () => {
    const steps = expandSteps([{ id: 'burst', repeat: 3, request }, { id: 'one', request }]);
    assert.deepEqual(steps.map((step) => step.id), ['burst#1', 'burst#2', 'burst#3', 'one']);
    assert.equal('repeat' in steps[0], false);
  });
});

describe('key placeholders', () => {
  const keys = { free: 'lodb_abc' };

  it('resolves placeholders in the path and in every header value', () => {
    const resolved = resolveRequest({
      method: 'GET',
      path: '/v1/usage?api_key={{key:free}}',
      headers: { Authorization: 'Bearer {{key:free}}', 'X-Api-Key': '  {{key:free}}  ' },
    }, keys);
    assert.equal(resolved.path, '/v1/usage?api_key=lodb_abc');
    assert.deepEqual(resolved.headers,
      { Authorization: 'Bearer lodb_abc', 'X-Api-Key': '  lodb_abc  ' });
  });

  it('fails on an unknown alias instead of sending the placeholder', () => {
    assert.throws(() => resolveKeys('{{key:gold}}', keys), /unknown key alias gold/);
  });
});
