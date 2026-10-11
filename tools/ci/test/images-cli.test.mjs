import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { afterEach, beforeEach, describe, it } from 'node:test';

const SCRIPT = fileURLToPath(new URL('../check-pinned-images.mjs', import.meta.url));
const DIGEST = `sha256:${'0123456789abcdef'.repeat(4)}`;
const PINNED = `FROM nginx:1.31-alpine@${DIGEST}\n`;

function check(args = []) {
  return spawnSync(process.execPath, [SCRIPT, ...args], { encoding: 'utf8' });
}

describe('check-pinned-images', () => {
  let root;
  const write = (path, text) => {
    mkdirSync(join(root, path, '..'), { recursive: true });
    writeFileSync(join(root, path), text);
  };

  beforeEach(() => {
    root = mkdtempSync(join(tmpdir(), 'lodb-pinned-images-'));
  });
  afterEach(() => rmSync(root, { recursive: true, force: true }));

  it('passes when every image is pinned, and reads every Dockerfile name', () => {
    write('docker/nginx/Dockerfile', PINNED);
    write('docker/nginx/Dockerfile.dockerignore', 'FROM ignored\n');
    write('docker/tools/Dockerfile.ci', PINNED);
    write('docker/tools/bench.Dockerfile', PINNED);

    const result = check(['--root', root]);

    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /3 images in 3 files, pinned/);
  });

  it('fails on an unpinned image and names its file and line', () => {
    write('docker/api/Dockerfile', `${PINNED}FROM node:24-slim\n`);

    const result = check(['--root', root]);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /Dockerfile:2: node:24-slim: not pinned to a sha256 digest/);
    assert.match(result.stderr, /1 of 2 image references break the pinning rules/);
  });

  it('fails when there is no Dockerfile to read', () => {
    const result = check(['--root', root]);

    assert.equal(result.status, 1);
    assert.match(result.stderr, /no Dockerfile under/);
  });

  it('passes on the Dockerfiles of this repository', () => {
    const result = check();

    assert.equal(result.status, 0, result.stderr);
  });
});
