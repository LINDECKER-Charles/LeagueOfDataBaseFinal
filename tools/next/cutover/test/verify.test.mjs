import assert from 'node:assert/strict';
import { test } from 'node:test';
import { pool, verify } from '../lib/verify.mjs';

const BASE = 'http://localhost:18280';

// A stack answering from a table: path → [status, location].
function stack(routes) {
  return async (url) => {
    const [status, location] = routes[`${url.pathname}${url.search}`] ?? [404, null];
    return new Response(null, { status, headers: location ? { location } : {} });
  };
}

function check(path, routes) {
  return verify(path, { base: BASE, timeoutMs: 1000, fetcher: stack(routes) });
}

test('passes one 301 to the new form, then a 200', async () => {
  const verdict = await check('/champion/Ahri', {
    '/champion/Ahri': [301, '/en/champions/Ahri'],
    '/en/champions/Ahri': [200],
  });
  assert.equal(verdict.ok, true);
  assert.equal(verdict.landing, 200);
});

test('fails a second hop, a wrong form or a temporary redirect', async () => {
  const twoHops = await check('/champions', {
    '/champions': [301, '/en/champions'],
    '/en/champions': [301, '/en/champions/'],
  });
  assert.equal(twoHops.ok, false);
  assert.match(twoHops.reason, /answered 301/);

  const wrongForm = await check('/object/1004', { '/object/1004': [301, '/en/items'] });
  assert.match(wrongForm.reason, /is not/);

  const temporary = await check('/faq', { '/faq': [302, '/en/faq'], '/en/faq': [200] });
  assert.match(temporary.reason, /expected 301, got 302/);
});

test('expects a 302 at the root and no redirect on a contract', async () => {
  const root = await check('/', { '/': [302, '/fr/'], '/fr/': [200] });
  assert.equal(root.ok, true);
  assert.equal((await check('/b/abc', { '/b/abc': [404] })).ok, true);
  assert.equal((await check('/v1/usage', { '/v1/usage': [301, '/en/v1'] })).ok, false);
});

test('fails what the table does not know, and a stack that does not answer', async () => {
  assert.match((await check('/nowhere', {})).reason, /no row/);
  const verdict = await verify('/faq', {
    base: BASE,
    timeoutMs: 1000,
    fetcher: async () => {
      throw new Error('connection refused');
    },
  });
  assert.match(verdict.reason, /no answer: connection refused/);
});

test('runs a pool in order, never above its size', async () => {
  let running = 0;
  let peak = 0;
  const results = await pool([5, 1, 3, 2], 2, async (value) => {
    running += 1;
    peak = Math.max(peak, running);
    await new Promise((resolve) => setTimeout(resolve, value));
    running -= 1;
    return value * 10;
  });
  assert.deepEqual(results, [50, 10, 30, 20]);
  assert.equal(peak, 2);
});
