import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  PROBLEMS,
  imageReferencesOf,
  pinningProblemOf,
  pinningProblemsOf,
} from '../lib/image-pinning.mjs';

const DIGEST = `sha256:${'0123456789abcdef'.repeat(4)}`;
const NODE = `node:24-slim@${DIGEST}`;
const SDK = `mcr.microsoft.com/dotnet/sdk:10.0@${DIGEST}`;

describe('imageReferencesOf', () => {
  it('reads the frontend, the bases and the image a COPY or a mount takes from', () => {
    const text = [
      `# syntax=docker/dockerfile:1.7@${DIGEST}`,
      '# Image lodb-api.',
      `FROM --platform=$BUILDPLATFORM ${SDK} AS build`,
      `COPY --from=${NODE} /usr/local/bin/node /usr/local/bin/node`,
      `RUN --mount=type=bind,from=alpine:3@${DIGEST},target=/a true`,
      `FROM ${NODE}`,
    ].join('\n');

    assert.deepEqual(imageReferencesOf(text), [
      { line: 1, value: `docker/dockerfile:1.7@${DIGEST}` },
      { line: 3, value: SDK },
      { line: 4, value: NODE },
      { line: 5, value: `alpine:3@${DIGEST}` },
      { line: 6, value: NODE },
    ]);
  });

  it('skips the stages of the file, by name or index, and scratch', () => {
    const text = [
      `FROM ${NODE} AS Build`,
      'FROM build AS test',
      'COPY --from=test /a /b',
      'COPY --from=0 /a /b',
      'RUN --mount=type=cache,from=build,target=/c true',
      'FROM scratch',
    ].join('\n');

    assert.deepEqual(imageReferencesOf(text), [{ line: 1, value: NODE }]);
  });

  it('joins continued lines and drops the comments between them', () => {
    const text = [
      'FROM \\',
      '  # the build stage',
      `  ${SDK} \\`,
      '  AS build',
    ].join('\n');

    assert.deepEqual(imageReferencesOf(text), [{ line: 1, value: SDK }]);
  });

  it('reads a syntax directive only at the top of the file', () => {
    const text = ['# Image lodb-api.', '# syntax=docker/dockerfile:1.7', `FROM ${NODE}`];

    assert.deepEqual(imageReferencesOf(text.join('\n')), [{ line: 3, value: NODE }]);
  });

  it('ignores a --from flag outside COPY, ADD and RUN mounts', () => {
    assert.deepEqual(imageReferencesOf('RUN tool --from=node:24 run'), []);
  });
});

describe('pinningProblemOf', () => {
  it('accepts a tag pinned to its digest, registry and port included', () => {
    assert.equal(pinningProblemOf(SDK), null);
    assert.equal(pinningProblemOf(`localhost:5000/team/app:1.2@${DIGEST}`), null);
  });

  it('refuses a tag alone', () => {
    assert.equal(pinningProblemOf('node:24-slim'), PROBLEMS.notPinned);
    assert.equal(pinningProblemOf('node'), PROBLEMS.notPinned);
  });

  it('refuses a short or malformed digest', () => {
    assert.equal(pinningProblemOf('node:24@sha256:0123'), PROBLEMS.notPinned);
    assert.equal(pinningProblemOf(`node:24@${DIGEST.toUpperCase()}`), PROBLEMS.notPinned);
  });

  it('refuses a digest without its tag', () => {
    assert.equal(pinningProblemOf(`node@${DIGEST}`), PROBLEMS.noTag);
    assert.equal(pinningProblemOf(`localhost:5000/app@${DIGEST}`), PROBLEMS.noTag);
  });

  it('refuses an image named through a variable', () => {
    assert.equal(pinningProblemOf(`node:\${NODE_VERSION}@${DIGEST}`), PROBLEMS.variable);
  });
});

describe('pinningProblemsOf', () => {
  it('reports each unpinned image with its line', () => {
    const text = ['# syntax=docker/dockerfile:1.7', `FROM ${NODE} AS build`, 'FROM nginx:1.31'];

    assert.deepEqual(pinningProblemsOf(text.join('\n')), [
      { line: 1, value: 'docker/dockerfile:1.7', problem: PROBLEMS.notPinned },
      { line: 3, value: 'nginx:1.31', problem: PROBLEMS.notPinned },
    ]);
  });

  it('reports nothing when every image is pinned', () => {
    assert.deepEqual(pinningProblemsOf(`FROM ${NODE} AS build\nFROM build`), []);
  });
});
