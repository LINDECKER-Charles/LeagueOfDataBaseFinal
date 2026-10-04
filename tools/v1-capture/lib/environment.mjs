// Lifecycle of the throwaway capture environment: a private Postgres loaded with the
// old-stack schema snapshot and the seed, and a go-api container built from legacy/go/api.
// Nothing here touches the old stack: no shared network, volume, database or port.
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { docker, ensureNetwork, isRunning, removeContainer, removeNetwork } from './docker.mjs';
import {
  containerStorageDir, containerVolumeDir, goApiDir, hiddenStorageSuffix, names, postgres,
  repoRoot, seedDir, timings,
} from './settings.mjs';

const seededTables = ['users', 'builds', 'api_keys', 'api_usage'];
const apiContainerPort = 8090;

export const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Polls check() until it returns true, or fails after the readiness timeout. */
export async function waitUntil(what, check) {
  const deadline = Date.now() + timings.readinessTimeoutMs;
  while (Date.now() < deadline) {
    if (await check()) {
      return;
    }
    await sleep(timings.pollIntervalMs);
  }
  throw new Error(`${what} not ready after ${timings.readinessTimeoutMs} ms`);
}

/** Git tree of legacy/go/api, suffixed when the working copy differs from it. */
export function goApiRevision() {
  const git = (args) => spawnSync('git', args, { cwd: repoRoot, encoding: 'utf8' });
  const tree = git(['rev-parse', 'HEAD:legacy/go/api']).stdout.trim();
  const dirty = git(['status', '--porcelain', '--', 'legacy/go/api']).stdout.trim() !== '';
  return dirty ? `${tree}+dirty` : tree;
}

/** Builds go-api from the repository sources under a tag of its own. */
export function buildImage(revision) {
  const tag = `${names.imageRepository}:${revision.slice(0, 12)}`;
  docker(['build', '--quiet', '--tag', tag, goApiDir]);
  return tag;
}

/** Runs SQL in the capture database, stopping at the first error. */
export function psql(sql) {
  const args = ['exec', '-i', names.database, 'psql', '-X', '-q', '-v', 'ON_ERROR_STOP=1'];
  docker([...args, '-U', postgres.user, '-d', postgres.database], { input: sql });
}

/** Starts the capture database (in memory) with the schema snapshot, if not running. */
export async function ensureDatabase() {
  ensureNetwork(names.network, names.label);
  if (isRunning(names.database)) {
    return;
  }
  removeContainer(names.database);
  docker(['run', '--detach', '--name', names.database, '--network', names.network,
    '--label', names.label, '--tmpfs', '/var/lib/postgresql/data',
    '--env', `POSTGRES_USER=${postgres.user}`, '--env', `POSTGRES_PASSWORD=${postgres.password}`,
    '--env', `POSTGRES_DB=${postgres.database}`, postgres.image]);
  // The init phase listens on the unix socket only: a TCP probe means the final server.
  await waitUntil('capture database', () => docker(['exec', names.database, 'pg_isready',
    '-h', '127.0.0.1', '-U', postgres.user, '-d', postgres.database],
  { allowFailure: true }).status === 0);
  psql(fs.readFileSync(path.join(seedDir, 'schema.sql'), 'utf8'));
}

/** Replaces the seeded tables with a fresh copy of the data set. */
export function resetData() {
  const truncate = `TRUNCATE ${seededTables.join(', ')} RESTART IDENTITY CASCADE;\n`;
  psql(truncate + fs.readFileSync(path.join(seedDir, 'dataset.sql'), 'utf8'));
}

/** Stops the capture database: the outage scenarios need it gone, not paused. */
export function stopDatabase() {
  docker(['stop', names.database]);
}

/**
 * Starts a fresh go-api process (empty key cache, buckets and trends cache).
 * @param {{ image: string, port: number, volumeDir: string }} options
 */
export async function startApi({ image, port, volumeDir }) {
  removeContainer(names.api);
  const databaseUrl = `postgresql://${postgres.user}:${postgres.password}`
    + `@${names.database}:5432/${postgres.database}`;
  docker(['run', '--detach', '--name', names.api, '--network', names.network,
    '--label', names.label, '--publish', `127.0.0.1:${port}:${apiContainerPort}`,
    '--env', `DATABASE_URL=${databaseUrl}`, '--env', `STORAGE_DIR=${containerStorageDir}`,
    '--volume', `${volumeDir}:${containerVolumeDir}`, image]);
  await waitUntil('go-api', () => isHealthy(port));
}

/**
 * Makes the storage directory disappear under the running API. The rename happens in
 * the container: renamed from the host, Docker Desktop's file-sharing cache would keep
 * serving part of the old tree for a while. That is also why the volume is mounted
 * read-write, although go-api never writes to it.
 */
export function hideStorage() {
  docker(['exec', '--user', 'root', names.api, 'mv', containerStorageDir,
    containerStorageDir + hiddenStorageSuffix]);
}

async function isHealthy(port) {
  try {
    const response = await fetch(`http://127.0.0.1:${port}/healthz`);
    const health = await response.json();
    return health.dependencies?.postgres === 'ok' && health.dependencies?.storage === 'ok';
  } catch {
    return false;
  }
}

/** Removes every container and the network the capture created. */
export function teardown() {
  removeContainer(names.api);
  removeContainer(names.database);
  removeNetwork(names.network);
}
