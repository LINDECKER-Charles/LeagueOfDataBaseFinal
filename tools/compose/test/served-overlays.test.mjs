// Checks what Compose makes of the served overlays, from the .env examples: the production
// stack's CPU quotas, weights and process caps (compose.deploy.yaml).
// Needs the Docker CLI only: `compose config` reads files, it starts nothing.
import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { after, before, describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const run = promisify(execFile);
const ROOT = resolve(fileURLToPath(new URL('../../../', import.meta.url)));
const SERVED = ['compose.yaml', 'compose.deploy.yaml'];
const RUNNING = ['api', 'web-ssr', 'nginx', 'postgres'];
// What the deploy job writes on a host, absent from the examples.
const HOST_ADDITIONS = 'LODB_EDGE_CIDR=172.18.0.0/16\n';
const IS_CALLER_SETTING = /^(?:LODB_|LoDb__|COMPOSE_)|^(?:IMAGE_TAG|APP_REVISION)$/;

const hasCompose = await run('docker', ['compose', 'version']).then(() => true, () => false);

// The caller's own environment outranks any env file in Compose: none of it may leak in.
function isolatedEnvironment() {
  return Object.fromEntries(Object.entries(process.env)
    .filter(([key]) => !IS_CALLER_SETTING.test(key)));
}

async function servicesOf(envFiles, files) {
  const args = ['compose', '--project-directory', ROOT, '-p', 'lodb-overlays-check',
    ...envFiles.flatMap((file) => ['--env-file', file]),
    ...files.flatMap((file) => ['-f', join(ROOT, file)]), 'config', '--format', 'json'];
  const { stdout } = await run('docker', args,
    { env: isolatedEnvironment(), maxBuffer: 16 * 1024 * 1024 });
  return JSON.parse(stdout).services;
}

const limitsOf = (service) => service.deploy?.resources?.limits ?? {};
const coresOf = (services) =>
  RUNNING.reduce((total, name) => total + Number(limitsOf(services[name]).cpus), 0);

describe('served overlays through Compose',
  { skip: hasCompose ? false : 'docker compose is not available' }, () => {
    let folder;
    let prod;

    before(async () => {
      folder = await mkdtemp(join(tmpdir(), 'lodb-overlays-'));
      await writeFile(join(folder, 'host.env'), HOST_ADDITIONS);
      const host = join(folder, 'host.env');
      prod = await servicesOf([join(ROOT, '.env.prod.example'), host], SERVED);
    });

    after(() => folder && rm(folder, { recursive: true, force: true }));

    test('bounds the CPU and the processes of every production service', () => {
      for (const name of RUNNING) {
        const limits = limitsOf(prod[name]);
        assert.ok(Number(limits.cpus) > 0, `${name} has a CPU quota`);
        assert.ok(limits.pids > 0, `${name} has a process cap`);
        assert.ok(prod[name].cpu_shares > 0, `${name} has a CPU weight`);
      }
      assert.ok(limitsOf(prod.migrate).pids > 0, 'migrate has a process cap');
      assert.ok(coresOf(prod) < 4, `${coresOf(prod)} cores leave the host some`);
      assert.ok(RUNNING.every((name) => prod.nginx.cpu_shares >= prod[name].cpu_shares));
    });
  });
