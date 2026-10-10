// Checks the bench overlay through Compose: applied after compose.override.yaml, it leaves no
// bound that the dev overlay lifts for the E2E suite at its lifted value (a lift added there
// later fails here until the overlay drops it too), and it gives each container a served
// host's memory limit and the planned CPU quotas.
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
const BASE = ['compose.yaml'];
const DEV = [...BASE, 'compose.override.yaml'];
const BENCH = [...DEV, 'tools/load/compose.bench.yaml'];
const HOST = [...BASE, 'compose.deploy.yaml'];
// What the dev overlay sets that is no bound: the OpenAPI documents (Development) and the
// stack's mail sink. Nothing else of it may reach a bench.
const DEV_SETTINGS = new Set([
  'ASPNETCORE_ENVIRONMENT',
  'LoDb__Mail__Host',
  'LoDb__Mail__Port',
  'LoDb__Mail__Security',
]);
// The quotas planned for a served host (4 shared vCPU): cpus, cpu_shares, pids per container.
const QUOTAS = {
  'web-ssr': { cpus: 1, shares: 1024, pids: 256 },
  api: { cpus: 1.25, shares: 1024, pids: 512 },
  postgres: { cpus: 0.5, shares: 1024, pids: 256 },
  nginx: { cpus: 0.25, shares: 2048, pids: 256 },
};
const LIMITED = ['api', 'migrate', 'web-ssr', 'nginx', 'postgres'];
// The relaxations the dev overlay holds today, at least: SSR admission, cold catalog gate,
// warm-up, ingestion budget, sign-ins, nginx rates. Fewer read means a broken reading.
const FEWEST_LIFTS = 30;
const IS_CALLER_SETTING = /^(?:LODB_|LoDb__|COMPOSE_)|^(?:IMAGE_TAG|APP_REVISION)$/;

const hasCompose = await run('docker', ['compose', 'version']).then(() => true, () => false);

// The caller's own environment outranks any env file in Compose: none of it may leak in.
function isolatedEnvironment() {
  return Object.fromEntries(Object.entries(process.env)
    .filter(([key]) => !IS_CALLER_SETTING.test(key)));
}

async function configOf(envFile, files) {
  const args = ['compose', '--project-directory', ROOT, '-p', 'lodb-bench-check',
    '--env-file', envFile, ...files.flatMap((file) => ['-f', join(ROOT, file)]),
    'config', '--format', 'json'];
  const { stdout } = await run('docker', args,
    { env: isolatedEnvironment(), maxBuffer: 16 * 1024 * 1024 });
  return JSON.parse(stdout);
}

// A key named without a value reaches no container: the same as no key at all.
function valueOf(environment, key) {
  return environment?.[key] ?? undefined;
}

// The keys the dev overlay sets or changes on the stack's own services (mailpit, a dev-only
// sink, is none of them).
function liftsOf(base, dev) {
  return Object.entries(dev.services)
    .filter(([service]) => service in base.services)
    .flatMap(([service, { environment = {} }]) => Object.keys(environment)
      .filter((key) => !DEV_SETTINGS.has(key))
      .filter((key) => valueOf(environment, key) !==
        valueOf(base.services[service]?.environment, key))
      .map((key) => ({ service, key })));
}

describe('bench overlay through Compose',
  { skip: hasCompose ? false : 'docker compose is not available' }, () => {
    let folder;
    let configs;

    before(async () => {
      folder = await mkdtemp(join(tmpdir(), 'lodb-bench-'));
      const local = join(folder, 'local.env');
      const host = join(folder, 'host.env');
      await writeFile(local, '');
      await writeFile(host, `${await readFile(join(ROOT, '.env.prod.example'), 'utf8')}\n` +
        'LODB_EDGE_CIDR=172.18.0.0/16\n');
      const [base, dev, bench, served] = await Promise.all([
        configOf(local, BASE), configOf(local, DEV), configOf(local, BENCH), configOf(host, HOST),
      ]);
      configs = { base, dev, bench, served };
    });

    after(() => folder && rm(folder, { recursive: true, force: true }));

    test("gives back compose.yaml's value of every bound the dev overlay lifts", () => {
      const lifts = liftsOf(configs.base, configs.dev);
      assert.ok(lifts.length >= FEWEST_LIFTS, `${lifts.length} lifted keys read`);
      for (const { service, key } of lifts) {
        assert.equal(valueOf(configs.bench.services[service].environment, key),
          valueOf(configs.base.services[service].environment, key), `${service} ${key}`);
      }
    });

    test('keeps of the dev overlay only settings it still makes', () => {
      const set = new Set(Object.values(configs.dev.services)
        .flatMap(({ environment = {} }) => Object.keys(environment)));
      for (const key of DEV_SETTINGS) {
        assert.ok(set.has(key), `${key} is no longer set by compose.override.yaml`);
      }
    });

    test("gives each container a served host's memory limit", () => {
      for (const service of LIMITED) {
        const limitOf = (config) => config.services[service].deploy?.resources?.limits?.memory;
        assert.ok(limitOf(configs.served), `${service} has a limit on a host`);
        assert.equal(limitOf(configs.bench), limitOf(configs.served), service);
      }
    });

    test('sets the CPU quotas planned for a served host, and its own where it has them', () => {
      for (const [service, quota] of Object.entries(QUOTAS)) {
        const { bench, served } = Object.fromEntries(['bench', 'served'].map((name) => {
          const config = configs[name].services[service];
          const limits = config.deploy?.resources?.limits ?? {};
          return [name, { cpus: limits.cpus, shares: config.cpu_shares, pids: limits.pids }];
        }));
        assert.deepEqual(bench, quota, service);
        for (const [key, value] of Object.entries(served)) {
          assert.ok(value === undefined || value === bench[key], `${service} ${key}`);
        }
      }
    });

    test("lets nginx believe X-Forwarded-For from the bench's network only", () => {
      const subnet = configs.bench.networks.default.ipam.config[0].subnet;
      assert.equal(configs.bench.services.nginx.environment.LODB_EDGE_CIDR, subnet);
      assert.match(subnet, /^10\./, 'outside the default pools of Docker');
    });
  });
