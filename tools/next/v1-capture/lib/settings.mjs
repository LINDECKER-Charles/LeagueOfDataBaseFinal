// Fixed names, paths and timings of the capture environment. Everything the tool creates
// carries the lodb-v1-capture prefix, so teardown can never reach another stack.
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const libDir = path.dirname(fileURLToPath(import.meta.url));

export const repoRoot = path.resolve(libDir, '../../../..');
export const goApiDir = path.join(repoRoot, 'go/api');
export const fixturesDir = path.join(repoRoot, 'tests/fixtures/v1');
export const seedDir = path.join(fixturesDir, 'seed');
export const scenariosDir = path.join(fixturesDir, 'scenarios');
export const referencesDir = path.join(fixturesDir, 'references');

export const names = Object.freeze({
  network: 'lodb-v1-capture',
  database: 'lodb-v1-capture-pg',
  api: 'lodb-v1-capture-api',
  imageRepository: 'lodb-v1-capture/go-api',
  label: 'lodb.v1-capture=1',
});

// Outside every port of the method (old stack, lodb-next and its slots, other projects).
export const defaultApiPort = 18990;

export const postgres = Object.freeze({
  image: 'postgres:17-alpine',
  user: 'lodb',
  password: 'lodb',
  database: 'lodb',
});

// Old-stack container the schema snapshot is refreshed from (read-only pg_dump).
export const defaultSchemaSource = 'lodb-postgres-1';

// go-api reads the volume under /srv/volume/storage so a step can hide the storage
// directory while the bind mount itself stays in place.
export const containerVolumeDir = '/srv/volume';
export const containerStorageDir = `${containerVolumeDir}/storage`;
export const hiddenStorageSuffix = '.hidden';

export const timings = Object.freeze({
  readinessTimeoutMs: 60_000,
  pollIntervalMs: 250,
  requestTimeoutMs: 15_000,
});

// Longest horizon of X-RateLimit-Reset: a bucket refills completely within one minute.
export const resetHorizonSeconds = 60;
