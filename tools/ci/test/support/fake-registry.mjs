// A registry kept in a folder, behind a `docker` stub first on the PATH
// (fixtures/docker-registry.sh), and a real git checkout: what tag-image.sh and
// promote-images.sh read and write, without a network.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import {
  chmodSync,
  copyFileSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { delimiter, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const ROOT = fileURLToPath(new URL('../../../..', import.meta.url));
export const PREFIX = 'ghcr.io/owner/lodb';
export const IMAGES = Object.freeze(['api', 'web-ssr', 'nginx']);
const DOCKER_STUB = join(ROOT, 'tools', 'ci', 'test', 'fixtures', 'docker-registry.sh');
const DIGEST_LENGTH = 64;
const workspaces = [];

/** A well-formed digest made of one repeated character. */
export const digestOf = (seed) => `sha256:${seed.repeat(DIGEST_LENGTH).slice(0, DIGEST_LENGTH)}`;

export function git(dir, ...args) {
  const identity = ['-c', 'user.name=ci', '-c', 'user.email=ci@example.test'];
  const result = spawnSync('git', [...identity, '-c', 'commit.gpgsign=false', ...args], {
    cwd: dir,
    encoding: 'utf8',
  });
  assert.equal(result.status, 0, result.stderr);
  return result.stdout.trim();
}

/** Writes one file and commits it; returns the new commit. */
export function commit(dir, path, text) {
  mkdirSync(join(dir, path, '..'), { recursive: true });
  writeFileSync(join(dir, path), text);
  git(dir, 'add', '--all');
  git(dir, 'commit', '-q', '-m', path);
  return git(dir, 'rev-parse', 'HEAD');
}

const imageFolder = (space, image) =>
  join(space.dir, 'registry', `${PREFIX}/${image}`.replaceAll('/', '_'));

/** A checkout whose first commit is `revision`, an empty registry and the docker stub. */
export function workspace() {
  const dir = mkdtempSync(join(tmpdir(), 'lodb-registry-'));
  workspaces.push(dir);
  const checkout = join(dir, 'checkout');
  mkdirSync(checkout);
  git(checkout, 'init', '-q');
  const revision = commit(checkout, 'src/app.txt', 'built\n');
  mkdirSync(join(dir, 'bin'));
  copyFileSync(DOCKER_STUB, join(dir, 'bin', 'docker'));
  chmodSync(join(dir, 'bin', 'docker'), 0o755);
  const space = { dir, checkout, revision };
  for (const image of IMAGES) {
    for (const folder of ['tags', 'blobs']) {
      mkdirSync(join(imageFolder(space, image), folder), { recursive: true });
    }
  }
  return space;
}

export function removeWorkspaces() {
  workspaces.splice(0).forEach((dir) => rmSync(dir, { recursive: true, force: true }));
}

/** Stores an image: its digest, and the revision label of its configuration. */
export function push(space, image, { digest, revision }) {
  const blob = join(imageFolder(space, image), 'blobs', digest.slice('sha256:'.length));
  writeFileSync(blob, `${revision}\n`);
}

export function tag(space, image, name, digest) {
  writeFileSync(join(imageFolder(space, image), 'tags', name), `${digest}\n`);
}

export function untag(space, image, name) {
  rmSync(join(imageFolder(space, image), 'tags', name));
}

/** The digest a tag names, or null. */
export function tagOf(space, image, name) {
  const path = join(imageFolder(space, image), 'tags', name);
  return existsSync(path) ? readFileSync(path, 'utf8').trim() : null;
}

/** Runs a script of the repository in the checkout, the stub first on the PATH. */
export function runScript(space, script, args = [], env = {}) {
  return spawnSync('bash', [join(ROOT, script), ...args], {
    cwd: space.checkout,
    encoding: 'utf8',
    env: {
      ...process.env,
      PATH: `${join(space.dir, 'bin')}${delimiter}${process.env.PATH}`,
      FAKE_REGISTRY: join(space.dir, 'registry'),
      ...env,
    },
  });
}
