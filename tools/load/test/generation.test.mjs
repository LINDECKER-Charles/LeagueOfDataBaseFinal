import assert from 'node:assert/strict';
import { describe, test } from 'node:test';
import { addressAt, crowdOf, VISITOR_ADDRESS } from '../lib/crowd.mjs';
import { optionsOf, PROBE, scenarioOf, thresholdsOf } from '../lib/plan.mjs';
import { cacheClassOf, warmUpEndOf } from '../lib/responses.mjs';
import { searchTermAt, tupleAt } from '../lib/walk.mjs';

describe('crowd', () => {
  test('gives the crawl of 2026-10-10 its 237 distinct public addresses', () => {
    const crowd = crowdOf(237);
    assert.equal(new Set(crowd).size, 237);
    assert.ok(crowd.every((address) => /^203\.0\.113\.(?:\d{1,3})$/.test(address)));
    assert.ok(!crowd.includes(VISITOR_ADDRESS), 'the human stands apart');
  });

  test('sends each client of the crowd as often as the others', () => {
    const crowd = crowdOf(5);
    const uses = new Map();
    for (let iteration = 0; iteration < 50; iteration += 1) {
      const address = addressAt(crowd, iteration);
      uses.set(address, (uses.get(address) ?? 0) + 1);
    }
    assert.deepEqual([...uses.values()], [10, 10, 10, 10, 10]);
  });

  test('refuses a crowd its network cannot hold', () => {
    assert.throws(() => crowdOf(0), RangeError);
    assert.throws(() => crowdOf(255), RangeError);
  });
});

describe('walk', () => {
  test('visits every combination once before any repeats, the same way every time', () => {
    const sizes = [3, 4, 5];
    const tuples = Array.from({ length: 60 }, (_, index) => tupleAt(sizes, index).join());
    assert.equal(new Set(tuples).size, 60);
    assert.deepEqual(tupleAt(sizes, 60), tupleAt(sizes, 0));
    assert.deepEqual(tupleAt(sizes, 17), tupleAt([3, 4, 5], 17));
  });

  test('changes every dimension from one step to the next on a large walk', () => {
    const sizes = [430, 21, 950];
    const [first, second] = [tupleAt(sizes, 1000), tupleAt(sizes, 1001)];
    first.forEach((digit, dimension) => assert.notEqual(digit, second[dimension]));
  });

  test('refuses an empty dimension', () => {
    assert.throws(() => tupleAt([3, 0], 1), RangeError);
  });

  test('types a different search of six letters each time', () => {
    const terms = Array.from({ length: 1000 }, (_, index) => searchTermAt(index));
    assert.ok(terms.every((term) => /^[a-z]{6}$/.test(term)));
    assert.ok(new Set(terms).size >= 995, `${new Set(terms).size} distinct terms`);
  });
});

describe('plan', () => {
  test('keeps the rates and crowds the scenarios model', () => {
    assert.deepEqual([scenarioOf('S1').rate, scenarioOf('S1').crowd], [23, 237]);
    assert.deepEqual([scenarioOf('S2').rate, scenarioOf('S2').crowd], [20, 1]);
    assert.equal(scenarioOf('S3').rate, 10);
    assert.equal(scenarioOf('S5').isStream, true);
    assert.equal(PROBE.rate, 1);
    assert.throws(() => scenarioOf('S6'), /Unknown scenario/);
  });

  test('runs the load and the probe side by side at a constant rate', () => {
    const { scenarios } = optionsOf('S1', '3m');
    assert.equal(scenarios.load.executor, 'constant-arrival-rate');
    assert.deepEqual([scenarios.load.rate, scenarios.load.duration], [23, '3m']);
    assert.deepEqual(scenarios.load.tags, { traffic: 'load' });
    assert.deepEqual([scenarios.probe.rate, scenarios.probe.exec], [1, 'probe']);
    assert.ok(scenarios.load.maxVUs >= scenarios.load.preAllocatedVUs);
    assert.equal(optionsOf('S4', '3m').maxRedirects, 0, 'a 301 is the answer measured');
  });

  test("judges the probe on the plan's thresholds and counts every status apart", () => {
    const thresholds = thresholdsOf();
    assert.deepEqual(thresholds.probe_hit_duration, ['p(95)<100']);
    assert.deepEqual(thresholds.probe_miss_duration, ['p(95)<1500']);
    assert.ok('http_reqs{traffic:load,status:503}' in thresholds);
    assert.ok('http_reqs{traffic:probe,status:200}' in thresholds);
  });
});

describe('responses', () => {
  test("tells a page the cache answered from one it waited for", () => {
    assert.equal(cacheClassOf('HIT'), 'hit');
    assert.equal(cacheClassOf('STALE'), 'hit');
    assert.equal(cacheClassOf('UPDATING'), 'hit');
    assert.equal(cacheClassOf('MISS'), 'miss');
    assert.equal(cacheClassOf('EXPIRED'), 'miss');
    assert.equal(cacheClassOf(undefined), 'none');
  });

  test('reads how a warm-up stream ended from its last frame', () => {
    const frame = (stage, extra = {}) => `data: ${JSON.stringify({ stage, ...extra })}\n\n`;
    assert.deepEqual(warmUpEndOf(frame('preparing') + frame('done')),
      { end: 'done', failure: null });
    assert.deepEqual(warmUpEndOf(frame('failed', { failure: 'catalog-pending' })),
      { end: 'failed', failure: 'catalog-pending' });
    assert.deepEqual(warmUpEndOf(frame('images')), { end: 'cut', failure: null });
    assert.deepEqual(warmUpEndOf('data: {broken\n\n'), { end: 'cut', failure: null });
    assert.deepEqual(warmUpEndOf(null), { end: 'cut', failure: null });
  });
});
