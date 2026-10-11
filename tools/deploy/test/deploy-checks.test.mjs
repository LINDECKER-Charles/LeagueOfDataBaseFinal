import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

// The remote script of _deploy.yml, from its start to the pull, run in a folder shaped like a
// host's: the dotenv reaches the .env and the secret files only once it passes the checks.
const ROOT = fileURLToPath(new URL('../../..', import.meta.url));
const WORKFLOW = join(ROOT, '.github', 'workflows', '_deploy.yml');
const SCRIPT = join(ROOT, 'tools', 'deploy', 'split-env.sh');
const HEREDOC = "<<'EOSSH'";
const FIRST_LINE = 'set -euo pipefail';
const STOP_MARKER = '# ── Pull, then migrations';
const EDGE_SUBNET = '172.18.0.0/16';
const OBSERVABILITY_SUBNET = '172.19.0.0/16';
// git and docker answer as on a host whose checkout, shared networks and Engine 25+ are in
// place; OBSERVABILITY_SUBNET in the environment moves that network onto another subnet.
const STUBS = [
  'git() { :; }',
  'docker() {',
  '  case "$*" in',
  '    "network inspect edge" | "network inspect observability") ;;',
  `    "network inspect edge --format "*) echo ${EDGE_SUBNET} ;;`,
  '    "network inspect observability --format "*)',
  `      echo "\${OBSERVABILITY_SUBNET:-${OBSERVABILITY_SUBNET}}" ;;`,
  '    "network ls --format "*) printf "edge\\nobservability\\n" ;;',
  '    "version --format "*) echo 1.47 ;;',
  '    "compose config api web-ssr") echo "      LODB_STACK_NETWORKS: $STACK_SUBNET" ;;',
  '    *) echo "unexpected: docker $*" >&2; return 1 ;;',
  '  esac',
  '}',
].join('\n');
const STAGING = {
  TARGET: 'staging',
  EXPECTED_PROJECT: 'lodb-staging',
  IMAGE_TAG: 'staging',
  EXPECTED_NOINDEX: '1',
  STACK_SUBNET: '10.231.1.0/24',
};
const PROD = {
  TARGET: 'prod',
  EXPECTED_PROJECT: 'lodb-prod',
  IMAGE_TAG: 'prod',
  EXPECTED_NOINDEX: '0',
  STACK_SUBNET: '10.231.2.0/24',
};
const workspaces = [];
after(() => workspaces.forEach((dir) => rmSync(dir, { recursive: true, force: true })));

function remoteScript() {
  const lines = readFileSync(WORKFLOW, 'utf8').split(/\r?\n/);
  const heredoc = lines.findIndex((line) => line.includes(HEREDOC));
  const start = lines.findIndex((line, index) => index > heredoc && line.trim() === FIRST_LINE);
  const stop = lines.findIndex((line) => line.includes(STOP_MARKER));
  assert.ok(heredoc > 0 && start > heredoc && stop > start, 'remote script not found');
  const indent = lines[start].indexOf(FIRST_LINE);
  return lines.slice(start, stop).map((line) => line.slice(indent)).join('\n');
}

function host() {
  const dir = mkdtempSync(join(tmpdir(), 'lodb-deploy-checks-'));
  workspaces.push(dir);
  mkdirSync(join(dir, '.git'));
  mkdirSync(join(dir, 'tools', 'deploy'), { recursive: true });
  mkdirSync(join(dir, '.deploy'));
  copyFileSync(SCRIPT, join(dir, 'tools', 'deploy', 'split-env.sh'));
  return dir;
}

// On its input, as ssh hands it over: a Windows command line would lose the script's quotes.
function deploy(dir, target, dotenv) {
  writeFileSync(join(dir, '.deploy', 'incoming.env'), dotenv);
  return spawnSync('bash', ['-s'], {
    input: `${STUBS}\n${remoteScript()}\n`,
    cwd: dir,
    encoding: 'utf8',
    env: { ...process.env, ...target, DEPLOY_PATH: '.', BRANCH: 'test', REPO_URL: 'unused' },
  });
}

function dotenvFor(target, lines = []) {
  return [
    `COMPOSE_PROJECT_NAME=${target.EXPECTED_PROJECT}`,
    `IMAGE_TAG=${target.IMAGE_TAG}`,
    `LODB_NOINDEX=${target.EXPECTED_NOINDEX}`,
    'LoDb__Mail__Host=smtp.example.test',
    'LODB_DB_PASSWORD=database-secret',
    'LoDb__Billing__StripeSecretKey=sk_live_example',
    'LoDb__Billing__StripeWebhookSecret=whsec_example',
    ...lines,
  ].join('\n');
}

const read = (dir, ...path) => readFileSync(join(dir, ...path), 'utf8');
const leftovers = (dir) =>
  readdirSync(join(dir, '.deploy')).filter((name) => name !== 'secrets');

function snapshot(dir) {
  return {
    env: read(dir, '.env'),
    password: read(dir, '.deploy', 'secrets', 'LODB_DB_PASSWORD'),
  };
}

describe('_deploy.yml, the dotenv against the target', () => {
  it('splits a dotenv that passes into the .env and the secret files', () => {
    const dir = host();

    const run = deploy(dir, PROD, dotenvFor(PROD));

    assert.equal(run.status, 0, run.stderr);
    assert.equal(read(dir, '.deploy', 'secrets', 'LODB_DB_PASSWORD'), 'database-secret');
    assert.match(read(dir, '.env'), /^COMPOSE_PROJECT_NAME=lodb-prod$/m);
    assert.match(read(dir, '.env'), /^LODB_SECRETS_REVISION=1$/m);
    assert.match(read(dir, '.env'), new RegExp(`^LODB_EDGE_CIDR=${EDGE_SUBNET}$`, 'm'));
    assert.match(read(dir, '.env'), /^COMPOSE_FILE=compose\.yaml:compose\.deploy\.yaml$/m);
    assert.match(read(dir, '.env'), /^LODB_STACK_SUBNET=10\.231\.2\.0\/24$/m);
    assert.doesNotMatch(read(dir, '.env'), /database-secret/);
    assert.deepEqual(leftovers(dir), []);
  });

  it('leaves the .env and the secret files in place when a network overlaps the stack', () => {
    const dir = host();
    assert.equal(deploy(dir, PROD, dotenvFor(PROD)).status, 0);
    const before = snapshot(dir);

    const run = deploy(dir, { ...PROD, OBSERVABILITY_SUBNET: '10.231.0.0/16' }, dotenvFor(PROD));

    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /overlaps 10\.231\.2\.0\/24/);
    assert.deepEqual(snapshot(dir), before);
    assert.deepEqual(leftovers(dir), []);
  });

  for (const [refusal, target, extra, other = target] of [
    ['the dotenv of another environment', PROD, ['LODB_DB_PASSWORD=other'], STAGING],
    ['prod without its webhook secret', PROD, ['LoDb__Billing__StripeWebhookSecret=']],
    ['any target without a database password', STAGING, ['LODB_DB_PASSWORD=']],
    ['a secret the split refuses', STAGING, ['LODB_DB_PASSWORD=pa$x']],
    ['a bundle retention shorter than a cached page', PROD, ['LODB_FRONT_BUILDS_RETENTION_DAYS=7']],
  ]) {
    it(`leaves the .env and the secret files in place for ${refusal}`, () => {
      const dir = host();
      assert.equal(deploy(dir, target, dotenvFor(target)).status, 0);
      const before = snapshot(dir);

      const run = deploy(dir, target, dotenvFor(other, extra));

      assert.notEqual(run.status, 0);
      assert.deepEqual(snapshot(dir), before);
      assert.deepEqual(leftovers(dir), []);
      assert.equal(existsSync(join(dir, '.deploy', 'incoming.env')), false);
    });
  }
});

describe("_deploy.yml, the edge's secret", () => {
  const EDGE_SECRET = /^[0-9a-f]{64}$/;
  const secretOf = (dir) => read(dir, '.deploy', 'secrets', 'LODB_EDGE_SECRET').trim();
  const edgeLines = (dir) => read(dir, '.env').split('\n')
    .filter((line) => line.startsWith('LODB_EDGE_SECRET='));

  it('generates it once on the host, hands it to the .env, and never prints it', () => {
    const dir = host();

    const first = deploy(dir, PROD, dotenvFor(PROD));
    const secret = secretOf(dir);
    const second = deploy(dir, PROD, dotenvFor(PROD));

    assert.equal(first.status, 0, first.stderr);
    assert.equal(second.status, 0, second.stderr);
    assert.match(secret, EDGE_SECRET);
    assert.equal(secretOf(dir), secret, 'kept across deploys');
    assert.deepEqual(edgeLines(dir), [`LODB_EDGE_SECRET=${secret}`]);
    for (const run of [first, second]) {
      assert.ok(!`${run.stdout}${run.stderr}`.includes(secret), 'printed');
    }
  });

  it('replaces a value that the dotenv names', () => {
    const dir = host();

    const run = deploy(dir, STAGING, dotenvFor(STAGING, ['LODB_EDGE_SECRET=from-the-dotenv']));

    assert.equal(run.status, 0, run.stderr);
    assert.deepEqual(edgeLines(dir), [`LODB_EDGE_SECRET=${secretOf(dir)}`]);
  });

  it('leaves the .env and the secret files in place when the kept one is malformed', () => {
    const dir = host();
    assert.equal(deploy(dir, PROD, dotenvFor(PROD)).status, 0);
    writeFileSync(join(dir, '.deploy', 'secrets', 'LODB_EDGE_SECRET'), 'not-hexadecimal\n');
    const before = snapshot(dir);

    const run = deploy(dir, PROD, dotenvFor(PROD));

    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /LODB_EDGE_SECRET must hold 64 hexadecimal digits/);
    assert.doesNotMatch(run.stderr, /not-hexadecimal/);
    assert.deepEqual(snapshot(dir), before);
    assert.deepEqual(leftovers(dir), []);
  });
});
