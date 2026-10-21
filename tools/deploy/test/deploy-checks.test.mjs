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
// place; OBSERVABILITY_SUBNET in the environment moves that network onto another subnet, and
// MISSING_NETWORK names a shared network the host lacks. `compose config api` states the
// pass's pace as prod resolves it (the floor, ceiling and fixed pace of the .env examples,
// the step left to the code); PACE_MIN, PACE_MAX, PACE_STEP and PACE_FIXED replace each one.
const STUBS = [
  'git() { :; }',
  'docker() {',
  '  case "$*" in',
  '    "network inspect ${MISSING_NETWORK:-}") return 1 ;;',
  '    "network inspect edge" | "network inspect observability") ;;',
  '    "network inspect sauvegardes") ;;',
  `    "network inspect edge --format "*) echo ${EDGE_SUBNET} ;;`,
  '    "network inspect observability --format "*)',
  `      echo "\${OBSERVABILITY_SUBNET:-${OBSERVABILITY_SUBNET}}" ;;`,
  '    "network ls --format "*) printf "edge\\nobservability\\n" ;;',
  '    "version --format "*) echo 1.47 ;;',
  '    "compose config api web-ssr web-ssr-prerender")',
  '      echo "      LODB_STACK_NETWORKS: $STACK_SUBNET" ;;',
  '    "compose config api")',
  '      echo "      LoDb__Prerender__Pace__MinRendersPerMinute: \\"${PACE_MIN:-30}\\""',
  '      echo "      LoDb__Prerender__Pace__MaxRendersPerMinute: \\"${PACE_MAX:-240}\\""',
  '      [ -z "${PACE_STEP:-}" ] ||',
  '        echo "      LoDb__Prerender__Pace__StepRendersPerMinute: \\"$PACE_STEP\\""',
  '      echo "      LoDb__Prerender__RendersPerMinute: \\"${PACE_FIXED:-120}\\"" ;;',
  '    *) echo "unexpected: docker $*" >&2; return 1 ;;',
  '  esac',
  '}',
].join('\n');
// The overlays prod deploys, as the job joins them into COMPOSE_FILE.
const PROD_FILES = 'compose.yaml:compose.deploy.yaml:compose.umami.yaml:compose.sauvegarde.yaml';
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
// The API's own database password: drawn on the host, never in the pushed dotenv.
const APP_PASSWORD = 'LODB_APP_DB_PASSWORD';
// The password of the off-host backups' role: optional, in infra-vps's format once set.
const BACKUP_PASSWORD = 'LoDb__Database__BackupRole__Password';
const HEX_PASSWORD = /^[0-9a-f]{64}$/;
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
    appPassword: read(dir, '.deploy', 'secrets', APP_PASSWORD),
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
    assert.ok(read(dir, '.env').split('\n').includes(`COMPOSE_FILE=${PROD_FILES}`));
    assert.match(read(dir, '.env'), /^LODB_STACK_SUBNET=10\.231\.2\.0\/24$/m);
    assert.doesNotMatch(read(dir, '.env'), /database-secret/);
    assert.deepEqual(leftovers(dir), []);
  });

  it("draws the API's database password on the first deploy, then keeps it, unprinted", () => {
    const dir = host();

    const first = deploy(dir, STAGING, dotenvFor(STAGING));
    const drawn = read(dir, '.deploy', 'secrets', APP_PASSWORD);
    const second = deploy(dir, STAGING, dotenvFor(STAGING));

    assert.equal(first.status, 0, first.stderr);
    assert.equal(second.status, 0, second.stderr);
    assert.match(drawn, HEX_PASSWORD);
    assert.equal(read(dir, '.deploy', 'secrets', APP_PASSWORD), drawn);
    assert.match(read(dir, '.env'), /^LODB_SECRETS_REVISION=1$/m);
    const printed = [first, second].map((run) => run.stdout + run.stderr).join('');
    assert.equal(printed.includes(drawn), false);
    assert.equal(read(dir, '.env').includes(drawn), false);
  });

  it("declares the site to Umami and joins the backups' network in prod only", () => {
    const dir = host();

    const run = deploy(dir, { ...STAGING, MISSING_NETWORK: 'sauvegardes' }, dotenvFor(STAGING));

    assert.equal(run.status, 0, run.stderr);
    assert.match(
      read(dir, '.env'),
      /^COMPOSE_FILE=compose\.yaml:compose\.deploy\.yaml:compose\.staging\.yaml$/m,
    );
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

  it("leaves the .env and the secret files in place when prod lacks the backups' network", () => {
    const dir = host();
    assert.equal(deploy(dir, PROD, dotenvFor(PROD)).status, 0);
    const before = snapshot(dir);

    const run = deploy(dir, { ...PROD, MISSING_NETWORK: 'sauvegardes' }, dotenvFor(PROD));

    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /'sauvegardes' is missing — deploy infra-vps first/);
    assert.deepEqual(snapshot(dir), before);
    assert.deepEqual(leftovers(dir), []);
  });

  for (const [refusal, target, extra, other = target] of [
    ['the dotenv of another environment', PROD, ['LODB_DB_PASSWORD=other'], STAGING],
    ['prod without its webhook secret', PROD, ['LoDb__Billing__StripeWebhookSecret=']],
    ['any target without a database password', STAGING, ['LODB_DB_PASSWORD=']],
    ['a secret the split refuses', STAGING, ['LODB_DB_PASSWORD=pa$x']],
    ['a bundle retention shorter than a cached page', PROD, ['LODB_FRONT_BUILDS_RETENTION_DAYS=30']],
    ["a dotenv that sets the API's database password", STAGING, [`${APP_PASSWORD}=from-github`]],
    ['a backup role password under 16 characters', PROD, [`${BACKUP_PASSWORD}=${'a'.repeat(15)}`]],
    ['a backup role password infra-vps refuses', PROD, [`${BACKUP_PASSWORD}=pass.word.of.20.char`]],
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

describe("_deploy.yml, the pre-rendering pass's pace", () => {
  it('accepts the pace of the .env examples, the step left to the code', () => {
    const run = deploy(host(), PROD, dotenvFor(PROD));

    assert.equal(run.status, 0, run.stderr);
  });

  for (const [refusal, pace, message] of [
    ['a floor above the ceiling', { PACE_MIN: '300' }, /floor \(300\).*ceiling \(240\)/],
    ['a step above the ceiling', { PACE_STEP: '241' }, /step \(241\).*ceiling \(240\)/],
    ['the code\'s own step above a low ceiling', { PACE_MIN: '5', PACE_MAX: '20' },
      /step \(30\).*ceiling \(20\)/],
    ['a fixed pace above the ceiling', { PACE_FIXED: '241' }, /fixed pace \(241\).*ceiling/],
    ['a setting that is no whole number', { PACE_MAX: 'fast' }, /whole numbers \(got 'fast'\)/],
  ]) {
    it(`stops the deploy before any container changes for ${refusal}`, () => {
      const run = deploy(host(), { ...PROD, ...pace }, dotenvFor(PROD));

      assert.notEqual(run.status, 0);
      assert.match(run.stderr, message);
    });
  }

  it('accepts a floor, a step and a fixed pace at the ceiling', () => {
    const pace = { PACE_MIN: '240', PACE_MAX: '240', PACE_STEP: '240', PACE_FIXED: '240' };

    const run = deploy(host(), { ...PROD, ...pace }, dotenvFor(PROD));

    assert.equal(run.status, 0, run.stderr);
  });
});

describe("_deploy.yml, the pass's renderer in the deploy", () => {
  const workflow = readFileSync(WORKFLOW, 'utf8');
  const lineOf = (text) => workflow.split(/\r?\n/).findIndex((line) => line.includes(text));

  it("checks the trusted networks of the pass's renderer as of the site's", () => {
    assert.match(workflow, /docker compose config api web-ssr web-ssr-prerender/);
  });

  it('starts the site first, ends the maintenance page, then starts the renderer', () => {
    const served = lineOf('"${served[@]}"');
    const filter = lineOf('grep -vx web-ssr-prerender');
    const end = lineOf('maintenance end');
    const pass = lineOf('--wait-timeout 180 web-ssr-prerender');
    assert.ok(filter > 0 && filter < served, 'the services but the renderer');
    assert.ok(served > 0 && served < end, 'the maintenance page ends after the site is up');
    assert.ok(end < pass, 'the renderer starts once the page is off');
    assert.match(workflow.split(/\r?\n/)[pass + 1], /::warning title=Prerender SSR::/);
  });
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

describe("_deploy.yml, the password of the backups' role", () => {
  const WELL_FORMED = 'ab'.repeat(32);
  const secretOf = (dir) => read(dir, '.deploy', 'secrets', BACKUP_PASSWORD);

  it('is optional, and reaches its file once set, unprinted', () => {
    const dir = host();

    const unset = deploy(dir, PROD, dotenvFor(PROD));
    const empty = secretOf(dir);
    const set = deploy(dir, PROD, dotenvFor(PROD, [`${BACKUP_PASSWORD}=${WELL_FORMED}`]));

    assert.equal(unset.status, 0, unset.stderr);
    assert.equal(set.status, 0, set.stderr);
    assert.equal(empty, '');
    assert.equal(secretOf(dir), WELL_FORMED);
    assert.equal(`${set.stdout}${set.stderr}`.includes(WELL_FORMED), false);
  });

  it('refuses a malformed one, naming the key only', () => {
    const dir = host();

    const run = deploy(dir, PROD, dotenvFor(PROD, [`${BACKUP_PASSWORD}=pass.word.of.20.char`]));

    assert.notEqual(run.status, 0);
    assert.match(run.stderr, new RegExp(`${BACKUP_PASSWORD}: 16 or more letters`));
    assert.doesNotMatch(`${run.stdout}${run.stderr}`, /pass\.word/);
  });
});
