// Checks what Compose makes of the served overlays, from the .env examples: the production
// stack's CPU quotas, weights and process caps (compose.deploy.yaml), the smaller share of
// the staging stack (compose.staging.yaml), and the optional login on the staging pages
// (compose.staging-auth.yaml), whose bcrypt hash must reach the edge's label intact.
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
const STAGING = [...SERVED, 'compose.staging.yaml'];
const STAGING_AUTH = [...STAGING, 'compose.staging-auth.yaml'];
const RUNNING = ['api', 'web-ssr', 'nginx', 'postgres'];
// What the deploy job writes on a host, absent from the examples.
const HOST_ADDITIONS = 'LODB_EDGE_CIDR=172.18.0.0/16\n';
// The staging share of the host: about three quarters of a core.
const STAGING_CORES = 0.75;
const HASH = '$2a$14$jM2G4xK0ZDa8W6PF31H9BOKCv81PEwu/7g692HVRaN72tRoi1jH52';
const AUTH_ENV = `LODB_STAGING_BASIC_AUTH_USER=tester\nLODB_STAGING_BASIC_AUTH_HASH='${HASH}'\n`;
const OPEN_PATHS = '/api/* /v1/* /webhooks/* /healthz /build/* /cdn/blobs/* /u/*';
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
    let staging;

    before(async () => {
      folder = await mkdtemp(join(tmpdir(), 'lodb-overlays-'));
      await writeFile(join(folder, 'host.env'), HOST_ADDITIONS);
      await writeFile(join(folder, 'auth.env'), AUTH_ENV);
      const host = join(folder, 'host.env');
      prod = await servicesOf([join(ROOT, '.env.prod.example'), host], SERVED);
      staging = await servicesOf([join(ROOT, '.env.staging.example'), host], STAGING);
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

    test('gives staging a smaller share, with work queues to match', () => {
      assert.ok(coresOf(staging) <= STAGING_CORES, `${coresOf(staging)} cores for staging`);
      for (const name of RUNNING) {
        assert.ok(staging[name].cpu_shares < prod[name].cpu_shares, `${name} weighs less`);
        assert.ok(Number(limitsOf(staging[name]).cpus) < Number(limitsOf(prod[name]).cpus));
      }
      const api = staging.api.environment;
      assert.equal(api.LoDb__Ingestion__SyncConcurrency, '1');
      assert.equal(api.LoDb__Egress__FetchConcurrency, '1');
      const ssr = staging['web-ssr'].environment;
      assert.ok(Number(ssr.LODB_PINNED_RENDER_MAX_IN_FLIGHT)
        < Number(ssr.LODB_RENDER_MAX_IN_FLIGHT), 'the pinned lane stays under the total');
      assert.equal(prod['web-ssr'].environment.LODB_RENDER_MAX_IN_FLIGHT, '');
    });

    test('asks for the staging login on the pages only, its hash intact', async () => {
      const services = await servicesOf([join(ROOT, '.env.staging.example'),
        join(folder, 'host.env'), join(folder, 'auth.env')], STAGING_AUTH);
      const labels = services.nginx.labels;
      assert.equal(labels['caddy_0.@lodb_pages.not'], `path ${OPEN_PATHS}`);
      assert.equal(labels['caddy_0.basic_auth'], '@lodb_pages');
      // `compose config` writes each `$` of a value doubled, as Compose reads it back.
      assert.equal(labels['caddy_0.basic_auth.tester'], HASH.replaceAll('$', () => '$$'));
      assert.equal(labels['caddy_1.basic_auth'], undefined, 'the API subdomain stays open');
    });

    test('refuses the staging login without its two settings', async () => {
      await assert.rejects(servicesOf([join(ROOT, '.env.staging.example'),
        join(folder, 'host.env')], STAGING_AUTH), /LODB_STAGING_BASIC_AUTH_USER/);
    });
  });
