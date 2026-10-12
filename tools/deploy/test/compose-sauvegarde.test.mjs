import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { join } from 'node:path';
import { before, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

// The overlays each target deploys, rendered by Compose from its dotenv template: prod's
// Postgres alone joins the host's off-host backups (compose.sauvegarde.yaml), under the alias
// prefixed by its stack, and nothing else of either stack reaches that network.
const ROOT = fileURLToPath(new URL('../../..', import.meta.url));
const NETWORK = 'sauvegardes';
const ALIAS = 'lodb-prod-postgres';
const TARGETS = {
  prod: {
    template: '.env.prod.example',
    files: ['compose.yaml', 'compose.deploy.yaml', 'compose.umami.yaml', 'compose.sauvegarde.yaml'],
    subnet: '10.231.2.0/24',
  },
  staging: {
    template: '.env.staging.example',
    files: ['compose.yaml', 'compose.deploy.yaml', 'compose.staging.yaml'],
    subnet: '10.231.1.0/24',
  },
};
// Written by the deploy job on the host (configuration.md); Compose requires them.
const hostAdditions = (subnet) => ({
  LODB_EDGE_CIDR: '172.18.0.0/16',
  LODB_STACK_SUBNET: subnet,
  LODB_EDGE_SECRET: '5e'.repeat(32),
});

const hasCompose = spawnSync('docker', ['compose', 'version'], { encoding: 'utf8' }).status === 0;

function render({ template, files, subnet }) {
  const run = spawnSync(
    'docker',
    [
      'compose',
      '--env-file',
      template,
      ...files.flatMap((file) => ['-f', file]),
      'config',
      '--format',
      'json',
    ],
    { cwd: ROOT, encoding: 'utf8', env: { ...process.env, ...hostAdditions(subnet) } },
  );
  assert.equal(run.status, 0, run.stderr);
  return JSON.parse(run.stdout);
}

const joining = (config) =>
  Object.entries(config.services)
    .filter(([, service]) => NETWORK in (service.networks ?? {}))
    .map(([name]) => name);

describe('compose.sauvegarde.yaml', { skip: !hasCompose && 'no docker compose' }, () => {
  let prod;
  let staging;
  before(() => {
    prod = render(TARGETS.prod);
    staging = render(TARGETS.staging);
  });

  it("puts prod's Postgres alone on the host's network, under its stack's alias", () => {
    const { postgres } = prod.services;

    assert.equal(prod.networks[NETWORK].external, true);
    assert.equal(prod.networks[NETWORK].name, NETWORK);
    assert.deepEqual(joining(prod), ['postgres']);
    assert.deepEqual(postgres.networks[NETWORK].aliases, [ALIAS]);
    assert.deepEqual(postgres.networks.data.aliases, ['lodb-postgres']);
  });

  it('leaves staging off that network', () => {
    assert.equal(NETWORK in staging.networks, false);
    assert.deepEqual(joining(staging), []);
  });
});
