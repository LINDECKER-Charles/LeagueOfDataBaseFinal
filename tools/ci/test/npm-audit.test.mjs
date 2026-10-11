import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  MAX_ALLOWANCE_DAYS,
  advisoriesOf,
  allowListProblemsOf,
  auditOutcomeOf,
} from '../lib/npm-audit.mjs';

const TODAY = '2026-10-11';
const GHSA = 'GHSA-68fv-2mgg-jv7q';
const OTHER = 'GHSA-w5hq-g745-h8pq';
const advisory = (name, id, severity) => ({
  source: 1241209,
  name,
  title: `${name} advisory`,
  url: `https://github.com/advisories/${id}`,
  severity,
  range: '<1.2.2',
});
// The shape of `npm audit --json` (npm 7+): the advisory under its own package, the packages
// that depend on it naming that package only.
const REPORT = {
  vulnerabilities: {
    'source-map-js': { name: 'source-map-js', via: [advisory('source-map-js', GHSA, 'high')] },
    postcss: { name: 'postcss', via: ['source-map-js'] },
    uuid: { name: 'uuid', via: [advisory('uuid', OTHER, 'moderate')] },
  },
};
const entry = (fields = {}) => ({
  advisory: GHSA,
  package: 'source-map-js',
  reason: 'reached only through the build-time CSS inliner',
  expires: '2026-11-30',
  ...fields,
});

describe('advisoriesOf', () => {
  it('reads each advisory once, with its GHSA id, and skips the dependents', () => {
    assert.deepEqual(
      advisoriesOf(REPORT).map(({ id, package: name, severity }) => [id, name, severity]),
      [
        [GHSA, 'source-map-js', 'high'],
        [OTHER, 'uuid', 'moderate'],
      ],
    );
  });

  it('falls back on the npm source id when the advisory has no GHSA URL', () => {
    const withoutUrl = { ...advisory('a', 'x', 'high'), url: '' };
    const report = { vulnerabilities: { a: { via: [withoutUrl] } } };

    assert.equal(advisoriesOf(report)[0].id, '1241209');
  });

  it('reads an empty report', () => {
    assert.deepEqual(advisoriesOf({ vulnerabilities: {} }), []);
  });
});

describe('allowListProblemsOf', () => {
  it('accepts complete entries that expire within the allowance', () => {
    assert.deepEqual(allowListProblemsOf([entry()], TODAY), []);
  });

  it('refuses what is not an array', () => {
    assert.equal(allowListProblemsOf({}, TODAY)[0].problem, 'the allow-list is not a JSON array');
  });

  it('names the missing fields', () => {
    const [problem] = allowListProblemsOf([entry({ reason: '', expires: undefined })], TODAY);

    assert.deepEqual(problem, { index: 0, problem: 'missing reason, expires' });
  });

  it('refuses a date that is not a calendar date', () => {
    for (const expires of ['30/11/2026', '2026-02-30', 'soon']) {
      const [problem] = allowListProblemsOf([entry({ expires })], TODAY);

      assert.match(problem.problem, /is not a YYYY-MM-DD date/, expires);
    }
  });

  it('refuses an expiry beyond the allowance', () => {
    const [problem] = allowListProblemsOf([entry({ expires: '2027-01-10' })], TODAY);

    assert.match(problem.problem, new RegExp(`more than ${MAX_ALLOWANCE_DAYS}`));
    assert.deepEqual(allowListProblemsOf([entry({ expires: '2027-01-09' })], TODAY), []);
  });
});

describe('auditOutcomeOf', () => {
  const outcome = (entries, level = 'high', today = TODAY) =>
    auditOutcomeOf(REPORT, entries, { level, today });

  it('blocks an advisory at or above the level that no entry names', () => {
    const { blocking, allowed } = outcome([]);

    assert.deepEqual(blocking.map((item) => [item.id, item.expired]), [[GHSA, null]]);
    assert.deepEqual(allowed, []);
  });

  it('ignores the advisories below the level', () => {
    assert.deepEqual(outcome([], 'critical').blocking, []);
    assert.equal(outcome([], 'moderate').blocking.length, 2);
  });

  it('accepts an advisory its entry names, up to and including the expiry day', () => {
    const { blocking, allowed } = outcome([entry({ expires: TODAY })]);

    assert.deepEqual(blocking, []);
    assert.deepEqual(allowed.map((item) => [item.id, item.expires]), [[GHSA, TODAY]]);
  });

  it('blocks again once the entry expired, and says so', () => {
    const { blocking } = outcome([entry({ expires: '2026-10-10' })]);

    assert.deepEqual(blocking.map((item) => item.expired), ['2026-10-10']);
  });

  it('does not accept the advisory for another package', () => {
    assert.equal(outcome([entry({ package: 'postcss' })]).blocking.length, 1);
  });

  it('lists the entries that name nothing reported at the level', () => {
    const gone = entry({ advisory: 'GHSA-2222-3333-4444' });

    assert.deepEqual(outcome([entry(), gone]).stale, [gone]);
  });
});
