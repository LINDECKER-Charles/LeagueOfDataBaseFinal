// Checks the environment Compose hands nginx, on a local stack (compose.yaml) and on a served
// host (compose.yaml + compose.deploy.yaml, its .env from the example): every variable the
// image defaults (docker/nginx/Dockerfile) arrives with a value, the image's own unless the
// host's .env sets one. A key passed without a value would not keep the default: Docker
// drops an image ENV that a container names bare, and nginx then refuses its configuration.
// Needs the Docker CLI only: `compose config` reads files, it starts nothing.
import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { after, before, describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const run = promisify(execFile);
const ROOT = resolve(fileURLToPath(new URL('../../../', import.meta.url)));
// A literal default, empty for LODB_EDGE_SECRET; LODB_REVISION, the image's revision, is left
// out.
const IMAGE_DEFAULT = /^\s*(LODB_[A-Z_]+)=([^\s\\$]*)(?=\s|$)/gm;
const ENV_LINE = /^([A-Za-z_][A-Za-z0-9_]*)=(.*)$/gm;
// Set on purpose per environment (compose.yaml), dropped on a host (compose.deploy.yaml).
const REVISION = 'LODB_REVISION';
// The deploy job writes the edge and stack subnets and the edge's secret; a host tunes one
// bound and leaves another empty.
const EDGE_SECRET = '5e'.repeat(32);
const HOST_ADDITIONS = 'LODB_EDGE_CIDR=172.18.0.0/16\nLODB_STACK_SUBNET=10.231.2.0/24\n'
  + `LODB_EDGE_SECRET=${EDGE_SECRET}\nLODB_PAGES_RATE=5r/s\nLODB_PAGES_BURST=\n`;
const IS_CALLER_SETTING = /^(?:LODB_|COMPOSE_)|^(?:IMAGE_TAG|APP_REVISION)$/;

const hasCompose = await run('docker', ['compose', 'version']).then(() => true, () => false);

async function imageDefaults() {
  const dockerfile = await readFile(join(ROOT, 'docker/nginx/Dockerfile'), 'utf8');
  return new Map([...dockerfile.matchAll(IMAGE_DEFAULT)].map(([, key, value]) => [key, value]));
}

/** Keys an env file sets to a non-empty value. */
function keysSetBy(envFile) {
  return new Set([...envFile.matchAll(ENV_LINE)]
    .filter(([, , value]) => value.trim() !== '')
    .map(([, key]) => key));
}

// The caller's own environment outranks any env file in Compose: none of it may leak in.
function isolatedEnvironment() {
  return Object.fromEntries(Object.entries(process.env)
    .filter(([key]) => !IS_CALLER_SETTING.test(key)));
}

async function nginxEnvironment(envFile, ...files) {
  const args = ['compose', '--project-directory', ROOT, '-p', 'lodb-nginx-check',
    '--env-file', envFile, ...files.flatMap((file) => ['-f', join(ROOT, file)]),
    'config', '--format', 'json'];
  const { stdout } = await run('docker', args,
    { env: isolatedEnvironment(), maxBuffer: 16 * 1024 * 1024 });
  return JSON.parse(stdout).services.nginx.environment;
}

describe('nginx environment through Compose',
  { skip: hasCompose ? false : 'docker compose is not available' }, () => {
    let folder;
    let defaults;
    let hostEnv;

    before(async () => {
      folder = await mkdtemp(join(tmpdir(), 'lodb-nginx-env-'));
      defaults = await imageDefaults();
      hostEnv = await readFile(join(ROOT, '.env.prod.example'), 'utf8') + HOST_ADDITIONS;
      await writeFile(join(folder, 'local.env'), '');
      await writeFile(join(folder, 'host.env'), hostEnv);
    });

    after(() => folder && rm(folder, { recursive: true, force: true }));

    test('reads the defaults of the image', () => {
      assert.equal(defaults.get('LODB_PAGES_RATE'), '20r/s');
      assert.equal(defaults.get('LODB_ARCHIVE_CACHE_MIN_USES'), '1');
      assert.equal(defaults.get('LODB_EDGE_SECRET'), '', 'no header asked by default');
      assert.equal(defaults.get('LODB_PAGES_STORE_ENABLED'), '0', 'no store asked by default');
      assert.ok(!defaults.has(REVISION), `${REVISION} is the image's revision`);
      assert.ok(defaults.size >= 16, `${defaults.size} defaults read`);
    });

    test("gives a local stack the image's defaults", async () => {
      const environment = await nginxEnvironment(join(folder, 'local.env'), 'compose.yaml');
      for (const [key, value] of defaults) {
        assert.equal(environment[key], value, key);
      }
    });

    test("gives a served host a value for each, the image's unless its .env sets one",
      async () => {
        const environment = await nginxEnvironment(join(folder, 'host.env'),
          'compose.yaml', 'compose.deploy.yaml');
        const setByHost = keysSetBy(hostEnv);
        for (const [key, value] of Object.entries(environment)) {
          assert.ok(!key.startsWith('LODB_') || value, `${key} reaches nginx without a value`);
        }
        assert.ok(!(REVISION in environment), `${REVISION} keeps the image's revision`);
        for (const [key, value] of defaults) {
          if (!setByHost.has(key)) {
            assert.equal(environment[key], value, key);
          }
        }
        assert.equal(environment.LODB_PAGES_RATE, '5r/s');
        assert.equal(environment.LODB_EDGE_SECRET, EDGE_SECRET);
      });
  });
