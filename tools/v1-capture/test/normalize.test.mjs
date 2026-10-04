import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  decodeBody, markers, markVolatile, normalizeReset, normalizeResponse,
} from '../lib/normalize.mjs';

const window = { from: 1_800_000_000, to: 1_800_000_001 };

describe('normalizeReset', () => {
  it('marks a Unix time within the bucket horizon', () => {
    assert.equal(normalizeReset(String(window.to + 6), window), markers.unixTime);
    assert.equal(normalizeReset(String(window.from), window), markers.unixTime);
  });

  it('keeps a visible difference for a value outside the horizon or not a number', () => {
    assert.equal(normalizeReset(String(window.to + 120), window), markers.invalidUnixTime);
    assert.equal(normalizeReset('6', window), markers.invalidUnixTime);
    assert.equal(normalizeReset('soon', window), markers.invalidUnixTime);
  });
});

describe('decodeBody', () => {
  it('parses JSON bodies, charset parameter included', () => {
    const decoded = decodeBody('application/json; charset=utf-8', '{"a":1}\n');
    assert.deepEqual(decoded, { body: { a: 1 } });
  });

  it('keeps plain text verbatim, trailing newline included', () => {
    const decoded = decodeBody('text/plain; charset=utf-8', '404 page not found\n');
    assert.deepEqual(decoded, { text: '404 page not found\n' });
  });

  it('records nothing for an empty body', () => {
    assert.deepEqual(decodeBody('application/json', ''), {});
  });
});

describe('normalizeResponse', () => {
  const raw = {
    status: 429,
    headers: {
      date: 'Sat, 26 Sep 2026 10:00:00 GMT',
      'content-length': '90',
      'x-ratelimit-reset': String(window.to + 6),
      'x-ratelimit-limit': '10',
      'content-type': 'application/json; charset=utf-8',
    },
    text: '{"error":{"code":"rate_limited","message":"m"}}\n',
    window,
  };

  it('keeps only the contract headers, in a fixed order', () => {
    const response = normalizeResponse(raw);
    assert.deepEqual(Object.keys(response.headers),
      ['content-type', 'x-ratelimit-limit', 'x-ratelimit-reset']);
    assert.equal(response.headers['x-ratelimit-reset'], markers.unixTime);
  });

  it('replaces the declared volatile paths', () => {
    const response = normalizeResponse(raw, ['body.error.message', 'headers.x-ratelimit-limit']);
    assert.equal(response.body.error.message, markers.volatile);
    assert.equal(response.headers['x-ratelimit-limit'], markers.volatile);
  });
});

describe('markVolatile', () => {
  it('ignores a path that does not exist', () => {
    const response = { body: { data: [] } };
    markVolatile(response, 'body.data.0.created_at');
    assert.deepEqual(response, { body: { data: [] } });
  });
});
