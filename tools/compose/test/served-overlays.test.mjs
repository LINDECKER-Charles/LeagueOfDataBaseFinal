// Checks what Compose makes of the served overlays, from the .env examples: the production
// stack's CPU quotas, weights and process caps (compose.deploy.yaml), its runtime hardening
// and networks (the pass renderer and the one-shots included), the index of states built
// before the API starts, the edge's route to nginx, the hosts the API
// answers, the smaller share of the staging stack (compose.staging.yaml), the optional login
// on the staging pages (compose.staging-auth.yaml), whose bcrypt hash must reach the edge's
// label intact, and the production's declaration to Umami (compose.umami.yaml).
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
const PROD_UMAMI = [...SERVED, 'compose.umami.yaml'];
const DASHBOARD = 'stats.league-of-data-base.com';
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
// The services that serve the visitors: their quotas make the stack's share of the host.
const VISITORS = ['api', 'web-ssr', 'nginx', 'postgres'];
// The pre-rendering pass's own renderer: idle CPU only, at the lowest weight of the host.
const PASS_SSR = 'web-ssr-prerender';
const RUNNING = [...VISITORS, PASS_SSR];
const PASS_SSR_ALIAS = 'lodb-web-ssr-prerender';
// What the deploy job writes on a host, absent from the examples.
const EDGE_SECRET = '5e'.repeat(32);
const HOST_ADDITIONS = 'LODB_EDGE_CIDR=172.18.0.0/16\nLODB_STACK_SUBNET=10.231.1.0/24\n'
  + `LODB_EDGE_SECRET=${EDGE_SECRET}\n`;
// The one-shots, run before the API starts: the migrations, then the index of states.
const ONE_SHOTS = ['migrate', 'states'];
// Every service of the stack, the one-shots included.
const SERVICES = [...RUNNING, ...ONE_SHOTS];
// The pass renderer's CPU weight and quota: prod, then the staging's smaller ones.
const PASS_SSR_LIMITS = {
  prod: { shares: 128, cpus: 1, memory: 704 * 1024 * 1024 },
  staging: { shares: 64, cpus: 0.25, memory: 704 * 1024 * 1024 },
};
// What postgres's entrypoint needs, as root, before it becomes `postgres`: none beyond
// Docker's defaults (DAC_READ_SEARCH would also open open_by_handle_at, compose.yaml).
const POSTGRES_CAPABILITIES = ['CHOWN', 'DAC_OVERRIDE', 'FOWNER', 'SETGID', 'SETUID'];
const NGINX_UPSTREAM = '{{upstreams 8080}}';
const NGINX_TEMP = '/var/cache/nginx/temp';
// The staging share of the host: about three quarters of a core.
const STAGING_CORES = 0.75;
// The staging one-shots' weight, under the production visitors they run beside.
const STAGING_ONE_SHOT_SHARES = 128;
const HASH = '$2a$14$jM2G4xK0ZDa8W6PF31H9BOKCv81PEwu/7g692HVRaN72tRoi1jH52';
const AUTH_ENV = `LODB_STAGING_BASIC_AUTH_USER=tester\nLODB_STAGING_BASIC_AUTH_HASH='${HASH}'\n`;
const OPEN_PATHS = '/api/* /v1/* /webhooks/* /healthz /build/* /cdn/blobs/* /u/*';
const IS_CALLER_SETTING = /^(?:LODB_|LoDb__|COMPOSE_)|^(?:IMAGE_TAG|APP_REVISION)$/;
// Called by these names inside the stack: the renderer, nginx's probes, the healthcheck.
const INTERNAL_HOSTS = ['lodb-api', 'localhost', '127.0.0.1'];

const hasCompose = await run('docker', ['compose', 'version']).then(() => true, () => false);

// The caller's own environment outranks any env file in Compose: none of it may leak in.
function isolatedEnvironment() {
  return Object.fromEntries(Object.entries(process.env)
    .filter(([key]) => !IS_CALLER_SETTING.test(key)));
}

async function configOf(envFiles, files) {
  const args = ['compose', '--project-directory', ROOT, '-p', 'lodb-overlays-check',
    ...envFiles.flatMap((file) => ['--env-file', file]),
    ...files.flatMap((file) => ['-f', join(ROOT, file)]), 'config', '--format', 'json'];
  const { stdout } = await run('docker', args,
    { env: isolatedEnvironment(), maxBuffer: 16 * 1024 * 1024 });
  return JSON.parse(stdout);
}

const servicesOf = async (envFiles, files) => (await configOf(envFiles, files)).services;
const networksOf = (service) => Object.keys(service.networks ?? {}).sort();

const limitsOf = (service) => service.deploy?.resources?.limits ?? {};
// The quotas that add up to the stack's share of the host: the visitors' services alone. The
// pass renderer is bounded apart, and weighs next to nothing under contention.
const coresOf = (services) =>
  VISITORS.reduce((total, name) => total + Number(limitsOf(services[name]).cpus), 0);

describe('served overlays through Compose',
  { skip: hasCompose ? false : 'docker compose is not available' }, () => {
    let folder;
    let served;
    let prod;
    let staging;

    before(async () => {
      folder = await mkdtemp(join(tmpdir(), 'lodb-overlays-'));
      await writeFile(join(folder, 'host.env'), HOST_ADDITIONS);
      await writeFile(join(folder, 'auth.env'), AUTH_ENV);
      const host = join(folder, 'host.env');
      served = await configOf([join(ROOT, '.env.prod.example'), host], SERVED);
      prod = served.services;
      staging = await servicesOf([join(ROOT, '.env.staging.example'), host], STAGING);
    });

    test('runs every service read-only, without capabilities nor a way to gain one', () => {
      for (const name of SERVICES) {
        const service = prod[name];
        assert.equal(service.read_only, true, `${name} is read-only`);
        assert.deepEqual(service.cap_drop, ['ALL'], `${name} drops every capability`);
        assert.deepEqual(service.security_opt, ['no-new-privileges:true'], name);
        assert.ok(service.tmpfs?.some((mount) => mount.startsWith('/tmp:size=')), name);
      }
      assert.deepEqual(prod.postgres.cap_add, POSTGRES_CAPABILITIES);
      for (const name of SERVICES.filter((service) => service !== 'postgres')) {
        assert.equal(prod[name].cap_add, undefined, `${name} takes no capability back`);
      }
      // What a slow client has not read waits on a volume, never in nginx's tmpfs
      // (docker/nginx/nginx.conf): nginx would not start without it.
      const temp = prod.nginx.volumes.find((mount) => mount.target === NGINX_TEMP);
      assert.equal(temp?.type, 'volume');
    });

    test('keeps Postgres on an internal network that nginx and the SSR are not on', () => {
      assert.equal(served.networks.data.internal, true);
      assert.deepEqual(networksOf(prod.postgres), ['data']);
      assert.deepEqual(prod.postgres.networks.data.aliases, ['lodb-postgres']);
      assert.deepEqual(networksOf(prod.api), ['data', 'default', 'observability']);
      for (const name of ONE_SHOTS) {
        assert.deepEqual(networksOf(prod[name]), ['data', 'default'], name);
      }
      assert.deepEqual(networksOf(prod.nginx), ['default', 'edge']);
      assert.deepEqual(networksOf(prod['web-ssr']), ['default', 'observability']);
      assert.deepEqual(networksOf(prod[PASS_SSR]), ['default', 'observability']);
      // The network nginx reaches the API from is the one the API trusts.
      const [{ subnet }] = served.networks.default.ipam.config;
      assert.equal(prod.api.environment.LoDb__Hosting__KnownProxyNetworks__0, subnet);
    });

    // The API serves an index (docs/produit/seo-une-page-par-etat.md § 4.1): built by its own
    // image, as its role, from the stored datasets it cannot change, under the visitors.
    test('builds the index of states before the API starts, reading the datasets only', () => {
      const { states, api } = prod;
      assert.deepEqual(states.command, ['catalog', 'states', 'build']);
      assert.equal(states.image, api.image, 'the API image, no extra build');
      assert.equal(states.restart, 'no');
      assert.equal(states.depends_on.migrate.condition, 'service_completed_successfully');
      assert.equal(api.depends_on.states.condition, 'service_completed_successfully');
      const storage = states.volumes.find((mount) => mount.target === '/srv/storage');
      assert.deepEqual([storage?.source, storage?.read_only], ['ddragon', true]);
      assert.match(states.environment.ConnectionStrings__LoDb, /Username=lodb_app;/);
      assert.equal(states.environment.LoDb__Storage__Root, '/srv/storage');
      assert.ok(states.cpu_shares > prod[PASS_SSR].cpu_shares, 'above the pass renderer');
      assert.ok(states.cpu_shares < api.cpu_shares, 'under the visitors it runs beside');
    });

    test('has the edge reach nginx on its port, with the header nginx requires', () => {
      const labels = prod.nginx.labels;
      for (const site of ['caddy_0', 'caddy_1']) {
        assert.equal(labels[`${site}.reverse_proxy`], NGINX_UPSTREAM, site);
        assert.equal(labels[`${site}.reverse_proxy.header_up`], `X-LoDb-Edge ${EDGE_SECRET}`);
      }
      assert.equal(prod.nginx.environment.LODB_EDGE_SECRET, EDGE_SECRET);
    });

    after(() => folder && rm(folder, { recursive: true, force: true }));

    test('bounds the CPU and the processes of every production service', () => {
      for (const name of RUNNING) {
        const limits = limitsOf(prod[name]);
        assert.ok(Number(limits.cpus) > 0, `${name} has a CPU quota`);
        assert.ok(limits.pids > 0, `${name} has a process cap`);
        assert.ok(prod[name].cpu_shares > 0, `${name} has a CPU weight`);
      }
      for (const name of ONE_SHOTS) {
        assert.ok(limitsOf(prod[name]).pids > 0, `${name} has a process cap`);
        assert.ok(limitsOf(prod[name]).memory > 0, `${name} has a memory limit`);
      }
      assert.ok(Number(limitsOf(prod.states).cpus) > 0, 'states has a CPU quota');
      assert.ok(coresOf(prod) < 4, `${coresOf(prod)} cores leave the host some`);
      assert.ok(RUNNING.every((name) => prod.nginx.cpu_shares >= prod[name].cpu_shares));
    });

    test("gives the pass renderer the lowest weight of the host, so it takes idle CPU only", () => {
      for (const [name, services] of [['prod', prod], ['staging', staging]]) {
        const expected = PASS_SSR_LIMITS[name];
        const pass = services[PASS_SSR];
        assert.equal(pass.cpu_shares, expected.shares, `${name} weight`);
        assert.equal(Number(limitsOf(pass).cpus), expected.cpus, `${name} quota`);
        assert.equal(Number(limitsOf(pass).memory), expected.memory, `${name} memory`);
        for (const other of SERVICES.filter((service) => service !== PASS_SSR)) {
          const weight = services[other].cpu_shares;
          assert.ok(weight === undefined || pass.cpu_shares < weight, `${name} ${other}`);
        }
      }
    });

    test('answers to its own alias only, and keeps nginx from depending on it', () => {
      assert.deepEqual(prod[PASS_SSR].networks.default.aliases, [PASS_SSR_ALIAS]);
      assert.deepEqual(prod['web-ssr'].networks.default.aliases, ['lodb-web-ssr']);
      assert.deepEqual(Object.keys(prod.nginx.depends_on).sort(), ['api', 'web-ssr']);
      assert.equal(prod.api.environment.LoDb__Prerender__SsrBaseUrl,
        `http://${PASS_SSR_ALIAS}:4000`);
    });

    test('has the pass renderer write nothing shared, and be scraped like web-ssr', () => {
      const pass = prod[PASS_SSR];
      assert.equal(pass.volumes, undefined, 'no volume: the bundles keep a single writer');
      assert.equal(pass.environment.LODB_FRONT_BUILDS_DIR, undefined);
      assert.equal(prod['web-ssr'].environment.LODB_FRONT_BUILDS_DIR, '/srv/front-builds');
      assert.deepEqual(pass.labels, prod['web-ssr'].labels);
      assert.equal(pass.labels['prometheus.scrape'], 'true');
      assert.equal(pass.labels['prometheus.port'], '9464');
      assert.equal(pass.ports, undefined, 'no published port');
      assert.equal(pass.image, prod['web-ssr'].image, 'the same image, no extra build');
    });

    test('gives staging a smaller share, with work queues to match', () => {
      assert.ok(coresOf(staging) <= STAGING_CORES, `${coresOf(staging)} cores for staging`);
      for (const name of RUNNING) {
        assert.ok(staging[name].cpu_shares < prod[name].cpu_shares, `${name} weighs less`);
        assert.ok(Number(limitsOf(staging[name]).cpus) < Number(limitsOf(prod[name]).cpus));
      }
      for (const name of ONE_SHOTS) {
        assert.equal(staging[name].cpu_shares, STAGING_ONE_SHOT_SHARES, `${name} weighs less`);
      }
      const api = staging.api.environment;
      assert.equal(api.LoDb__Ingestion__SyncConcurrency, '1');
      assert.equal(api.LoDb__Egress__FetchConcurrency, '1');
      const ssr = staging['web-ssr'].environment;
      assert.ok(Number(ssr.LODB_PINNED_RENDER_MAX_IN_FLIGHT)
        < Number(ssr.LODB_RENDER_MAX_IN_FLIGHT), 'the pinned lane stays under the total');
      assert.equal(prod['web-ssr'].environment.LODB_RENDER_MAX_IN_FLIGHT, '');
    });

    // The renderer's copy of /api/meta: its default in code, or the host's `.env` value.
    test('hands the renderer the lifetime of its /api/meta copy from the host', async () => {
      assert.equal(prod['web-ssr'].environment.LODB_API_META_TTL_SECONDS, '');
      const meta = join(folder, 'meta.env');
      await writeFile(meta, 'LODB_API_META_TTL_SECONDS=15\n');
      const services = await servicesOf(
        [join(ROOT, '.env.prod.example'), join(folder, 'host.env'), meta], SERVED);
      assert.equal(services['web-ssr'].environment.LODB_API_META_TTL_SECONDS, '15');
    });

    test('lets the API answer its public hosts and its stack names only', () => {
      const hostsOf = (setting) => setting.split(/[\s,;]/).map((host) => host.trim())
        .filter((host) => host.length > 0);
      for (const [name, services] of [['prod', prod], ['staging', staging]]) {
        const hosts = hostsOf(services.api.environment.AllowedHosts);
        const labels = services.nginx.labels;
        const routed = hostsOf(`${labels.caddy_0},${labels.caddy_1}`);
        assert.deepEqual(hosts, [...routed, ...INTERNAL_HOSTS], `${name} AllowedHosts`);
        assert.ok(!hosts.includes('*'), `${name} filters the hosts`);
      }
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

    test('declares the production site to Umami, its dashboard and its pull', async () => {
      const services = await servicesOf([join(ROOT, '.env.prod.example'),
        join(folder, 'host.env')], PROD_UMAMI);
      const labels = services.nginx.labels;
      const website = labels['umami.website'];
      assert.match(website, UUID);
      assert.equal(labels['caddy_0.import'], 'umami');
      assert.equal(labels['umami.domain'], 'league-of-data-base.com');
      assert.equal(labels.caddy_2, DASHBOARD);
      assert.equal(labels['caddy_2.import'], `umami-tableau ${website}`);
      assert.equal(labels['caddy_1.import'], undefined, 'the API subdomain has no audience');
      assert.equal(labels['caddy_0.reverse_proxy'], prod.nginx.labels['caddy_0.reverse_proxy']);
      const api = services.api.environment;
      assert.equal(api.LoDb__Audience__WebsiteId, website);
      assert.equal(api.LoDb__Audience__Host, DASHBOARD);
      // The overlay comes after compose.deploy.yaml: its resets of the private ranges hold.
      assert.equal(api.LoDb__Hosting__KnownProxyNetworks__1, undefined);
    });

    test('leaves staging out of Umami', () => {
      assert.equal(Object.keys(staging.nginx.labels).some((key) => key.startsWith('umami.')),
        false);
      assert.equal(staging.api.environment.LoDb__Audience__WebsiteId, undefined);
    });

    test('refuses the staging login without its two settings', async () => {
      await assert.rejects(servicesOf([join(ROOT, '.env.staging.example'),
        join(folder, 'host.env')], STAGING_AUTH), /LODB_STAGING_BASIC_AUTH_USER/);
    });
  });
