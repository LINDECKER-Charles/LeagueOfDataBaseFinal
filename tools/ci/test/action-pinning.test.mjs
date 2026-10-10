import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  PROBLEMS,
  pinningProblemOf,
  pinningProblemsOf,
  usesReferencesOf,
} from '../lib/action-pinning.mjs';

const SHA = '3d3c42e5aac5ba805825da76410c181273ba90b1';
const DIGEST = `sha256:${'0123456789abcdef'.repeat(4)}`;

const problemOf = (value, comment = 'v1.2.3') => pinningProblemOf({ value, comment });

describe('usesReferencesOf', () => {
  it('reads step, list item and job references with their line and comment', () => {
    const text = [
      'jobs:',
      '  build:',
      '    uses: ./.github/workflows/_build.yml',
      '  test:',
      '    steps:',
      `      - uses: actions/checkout@${SHA} # v7.0.1`,
      '      - name: Setup',
      `        uses: "actions/setup-node@${SHA}"   #   v7.1.0  `,
    ].join('\n');

    assert.deepEqual(usesReferencesOf(text), [
      { line: 3, value: './.github/workflows/_build.yml', comment: '' },
      { line: 6, value: `actions/checkout@${SHA}`, comment: 'v7.0.1' },
      { line: 8, value: `actions/setup-node@${SHA}`, comment: 'v7.1.0' },
    ]);
  });

  it('skips comment lines and keys that only end in uses', () => {
    const text = ['# uses: actions/checkout@v7', 'reuses: actions/checkout@v7'].join('\n');

    assert.deepEqual(usesReferencesOf(text), []);
  });

  it('keeps an unparsed mention of a uses key, without a comment', () => {
    const [reference] = usesReferencesOf('      - { name: x, uses: actions/checkout@v7 }');

    assert.equal(reference.comment, null);
    assert.equal(reference.line, 1);
  });

  it('keeps the other spellings of a uses key, without a comment', () => {
    for (const line of [
      '      - "uses": actions/checkout@v7',
      "      - 'uses': actions/checkout@v7",
      '      - uses : actions/checkout@v7',
      '      - Uses: actions/checkout@v7',
      '      - { "uses": actions/checkout@v7 }',
      '      - ? uses',
      `      - "uses": actions/checkout@${SHA} # v7.0.1`,
    ]) {
      const references = usesReferencesOf(line);

      assert.equal(references.length, 1, line);
      assert.equal(pinningProblemOf(references[0]), PROBLEMS.unrecognised, line);
    }
  });

  it('reads CRLF files line by line', () => {
    const references = usesReferencesOf(`a: b\r\n  uses: actions/checkout@${SHA} # v7\r\n`);

    assert.deepEqual(references, [{ line: 2, value: `actions/checkout@${SHA}`, comment: 'v7' }]);
  });
});

describe('pinningProblemOf', () => {
  it('accepts an action or a reusable workflow pinned to a full SHA with its version', () => {
    assert.equal(problemOf(`actions/checkout@${SHA}`), null);
    assert.equal(problemOf(`owner/repo/.github/workflows/release.yml@${SHA}`), null);
  });

  it('accepts local actions and reusable workflows as they are', () => {
    assert.equal(problemOf('./.github/workflows/_deploy.yml', ''), null);
    assert.equal(problemOf('./.github/actions/setup', ''), null);
  });

  it('refuses tags, branches, short and upper case SHAs', () => {
    for (const value of [
      'actions/checkout@v7',
      'actions/checkout@v7.0.1',
      'actions/checkout@main',
      `actions/checkout@${SHA.slice(0, 12)}`,
      `actions/checkout@${SHA.toUpperCase()}`,
      `actions/checkout@${SHA}0`,
      'actions/checkout',
    ]) {
      assert.equal(problemOf(value), PROBLEMS.notPinned, value);
    }
  });

  it('refuses a pinned action whose version is not given in a comment', () => {
    assert.equal(problemOf(`actions/checkout@${SHA}`, ''), PROBLEMS.noVersionComment);
    assert.equal(problemOf(`actions/checkout@${SHA}`, '   '), PROBLEMS.noVersionComment);
  });

  it('accepts a container image only with its digest', () => {
    assert.equal(problemOf(`docker://rhysd/actionlint:1.7.12@${DIGEST}`), null);
    assert.equal(problemOf('docker://rhysd/actionlint:1.7.12'), PROBLEMS.imageNotPinned);
    assert.equal(problemOf('docker://alpine@sha256:abc'), PROBLEMS.imageNotPinned);
  });

  it('refuses a reference it could not parse', () => {
    assert.equal(pinningProblemOf({ value: 'x', comment: null }), PROBLEMS.unrecognised);
  });
});

describe('pinningProblemsOf', () => {
  it('lists only the references that break the rules', () => {
    const text = [
      `      - uses: actions/checkout@${SHA} # v7.0.1`,
      '      - uses: actions/setup-node@v7',
      '    uses: ./.github/workflows/_build.yml',
      `      - uses: azure/login@${SHA}`,
    ].join('\n');

    assert.deepEqual(
      pinningProblemsOf(text).map(({ line, problem }) => [line, problem]),
      [
        [2, PROBLEMS.notPinned],
        [4, PROBLEMS.noVersionComment],
      ],
    );
  });
});
