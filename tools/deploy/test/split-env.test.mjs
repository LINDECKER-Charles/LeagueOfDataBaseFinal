import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const SCRIPT = fileURLToPath(new URL('../split-env.sh', import.meta.url));
const KEYS = [
  'LODB_DB_PASSWORD',
  'LoDb__DataProtection__CertificatePassword',
  'LoDb__Mail__Password',
  'LoDb__Billing__StripeSecretKey',
  'LoDb__Billing__StripeWebhookSecret',
  'LoDb__Accounts__Google__ClientSecret',
  'LoDb__Accounts__Google__AppClients__0__ClientSecret',
  'LoDb__Accounts__Google__AppClients__1__ClientSecret',
  'LoDb__Analytics__VisitorKey',
];
const GENERATED = 'LODB_APP_DB_PASSWORD';
const HEX_SECRET = /^[0-9a-f]{64}$/;
const IS_POSIX = process.platform !== 'win32';
const workspaces = [];

after(() => workspaces.forEach((dir) => rmSync(dir, { recursive: true, force: true })));

function workspace() {
  const dir = mkdtempSync(join(tmpdir(), 'lodb-split-env-'));
  workspaces.push(dir);
  return { dir, env: join(dir, '.env'), secrets: join(dir, '.deploy', 'secrets') };
}

function split(place, dotenv) {
  return spawnSync('bash', [SCRIPT, place.env, place.secrets], { input: dotenv, encoding: 'utf8' });
}

function splitOk(place, dotenv) {
  const run = split(place, dotenv);
  assert.equal(run.status, 0, run.stderr);
  return run;
}

const secret = (place, key) => readFileSync(join(place.secrets, key), 'utf8');
const envLines = (place) => readFileSync(place.env, 'utf8').split('\n');
const revisionOf = (place) =>
  envLines(place).find((line) => line.startsWith('LODB_SECRETS_REVISION='));

describe('split-env.sh', () => {
  it('lists the secret keys', () => {
    const run = spawnSync('bash', [SCRIPT, '--keys'], { encoding: 'utf8' });

    assert.equal(run.status, 0, run.stderr);
    assert.deepEqual(run.stdout.trim().split('\n'), KEYS);
  });

  it('lists the secrets it generates', () => {
    const run = spawnSync('bash', [SCRIPT, '--generated'], { encoding: 'utf8' });

    assert.equal(run.status, 0, run.stderr);
    assert.deepEqual(run.stdout.trim().split('\n'), [GENERATED]);
  });

  it('moves every secret to its own file and keeps every other line in order', () => {
    const place = workspace();
    const dotenv = [
      '# Production',
      'COMPOSE_PROJECT_NAME=lodb-prod',
      'LODB_DB_PASSWORD=database-secret',
      '',
      '# LoDb__Mail__Password=a commented example stays a comment',
      'LoDb__Mail__From="LeagueOfDataBase <no-reply@example.test>"',
      'LoDb__Billing__StripeSecretKey=sk_live_example',
      "LODB_ADMIN_PASSWORD='kept $ in the .env'",
    ].join('\n');

    const run = splitOk(place, `${dotenv}\n`);

    assert.deepEqual(envLines(place), [
      '# Production',
      'COMPOSE_PROJECT_NAME=lodb-prod',
      '',
      '# LoDb__Mail__Password=a commented example stays a comment',
      'LoDb__Mail__From="LeagueOfDataBase <no-reply@example.test>"',
      "LODB_ADMIN_PASSWORD='kept $ in the .env'",
      'LODB_SECRETS_REVISION=1',
      '',
    ]);
    assert.deepEqual(readdirSync(place.secrets).sort(), [...KEYS, GENERATED].sort());
    assert.equal(secret(place, 'LODB_DB_PASSWORD'), 'database-secret');
    assert.equal(secret(place, 'LoDb__Billing__StripeSecretKey'), 'sk_live_example');
    assert.match(run.stdout, /2 of 9 secrets set, 1 generated, revision 1\./);
    assert.doesNotMatch(run.stdout + run.stderr, /database-secret|sk_live_example/);
  });

  it('reads each value as Compose does', () => {
    const place = workspace();
    const cases = {
      LODB_DB_PASSWORD: ['LODB_DB_PASSWORD=a=b==c', 'a=b==c'],
      LoDb__Mail__Password: ["LoDb__Mail__Password=' $pa ss \"#w0rd '", ' $pa ss "#w0rd '],
      LoDb__Billing__StripeSecretKey: [
        `LoDb__Billing__StripeSecretKey=" it's spaced "  # a comment`,
        " it's spaced ",
      ],
      LoDb__Billing__StripeWebhookSecret: [
        'LoDb__Billing__StripeWebhookSecret=   whsec_x#y  # a comment  ',
        'whsec_x#y',
      ],
      LoDb__Analytics__VisitorKey: ['export LoDb__Analytics__VisitorKey = key', 'key'],
      LoDb__Accounts__Google__ClientSecret: ["LoDb__Accounts__Google__ClientSecret=''", ''],
      LoDb__DataProtection__CertificatePassword: ['LoDb__DataProtection__CertificatePassword=', ''],
      LoDb__Accounts__Google__AppClients__0__ClientSecret: [
        'LoDb__Accounts__Google__AppClients__0__ClientSecret= #hash-first\t#tab',
        '#hash-first\t#tab',
      ],
    };

    splitOk(
      place,
      Object.values(cases)
        .map(([line]) => line)
        .join('\n'),
    );

    for (const [key, [, expected]] of Object.entries(cases)) {
      assert.equal(secret(place, key), expected, key);
    }
    assert.equal(secret(place, 'LoDb__Accounts__Google__AppClients__1__ClientSecret'), '');
  });

  it('drops the carriage returns of a dotenv saved on Windows', () => {
    const place = workspace();

    splitOk(place, 'COMPOSE_PROJECT_NAME=lodb-staging\r\nLODB_DB_PASSWORD=secret\r\n');

    assert.equal(secret(place, 'LODB_DB_PASSWORD'), 'secret');
    assert.deepEqual(envLines(place).slice(0, 1), ['COMPOSE_PROJECT_NAME=lodb-staging']);
  });

  it('takes the last assignment of a key, as Compose does', () => {
    const place = workspace();

    splitOk(place, 'LODB_DB_PASSWORD=first\nLODB_DB_PASSWORD=second');

    assert.equal(secret(place, 'LODB_DB_PASSWORD'), 'second');
  });

  for (const [form, line] of [
    ['a $ outside quotes', 'LODB_DB_PASSWORD=pa$word'],
    ['a $ inside double quotes', 'LODB_DB_PASSWORD="pa${word}"'],
    ['a backslash inside double quotes', 'LODB_DB_PASSWORD="pa\\nword"'],
    ['a quote left open', "LODB_DB_PASSWORD='password"],
    ['characters after the closing quote', 'LODB_DB_PASSWORD="pass"word'],
    ['a quote Compose reads as escaped', "LODB_DB_PASSWORD='pa\\' # word'"],
    ['a secret written KEY: value', 'LODB_DB_PASSWORD: pa-word'],
  ]) {
    it(`refuses ${form}, naming the key only, and writes nothing`, () => {
      const place = workspace();
      splitOk(place, 'LODB_DB_PASSWORD=before\n');

      const run = split(place, `COMPOSE_PROJECT_NAME=changed\n${line}\n`);

      assert.notEqual(run.status, 0);
      assert.match(run.stderr, /LODB_DB_PASSWORD/);
      assert.doesNotMatch(run.stderr, /pa.?.?word|password/);
      assert.equal(secret(place, 'LODB_DB_PASSWORD'), 'before');
      assert.doesNotMatch(readFileSync(place.env, 'utf8'), /changed/);
    });
  }

  it('raises the revision only when a secret file changes', () => {
    const place = workspace();
    const dotenv = 'LODB_DB_PASSWORD=one\nLoDb__Mail__Password=mail\n';

    splitOk(place, dotenv);
    splitOk(place, `${dotenv}LODB_SECRETS_REVISION=40\nCOMPOSE_PROJECT_NAME=lodb-prod\n`);
    const unchanged = revisionOf(place);
    splitOk(place, dotenv.replace('one', 'two'));
    const rotated = revisionOf(place);
    splitOk(place, 'LODB_DB_PASSWORD=two\n');

    assert.deepEqual(
      [unchanged, rotated, revisionOf(place)],
      ['LODB_SECRETS_REVISION=1', 'LODB_SECRETS_REVISION=2', 'LODB_SECRETS_REVISION=3'],
    );
    assert.equal(envLines(place).filter((line) => line.startsWith('LODB_SECRETS')).length, 1);
  });

  it('draws the API database password once, then keeps it', () => {
    const place = workspace();
    const dotenv = 'LODB_DB_PASSWORD=one\n';

    const first = splitOk(place, dotenv);
    const drawn = secret(place, GENERATED);
    const second = splitOk(place, dotenv);

    assert.match(drawn, HEX_SECRET);
    assert.equal(secret(place, GENERATED), drawn);
    assert.equal(revisionOf(place), 'LODB_SECRETS_REVISION=1');
    assert.match(second.stdout, /0 generated, revision 1\./);
    assert.doesNotMatch(first.stdout + first.stderr + second.stdout, new RegExp(drawn));
  });

  it('draws a new one when its file is gone, and raises the revision', () => {
    const place = workspace();
    splitOk(place, 'LODB_DB_PASSWORD=one\n');
    const drawn = secret(place, GENERATED);
    rmSync(join(place.secrets, GENERATED), { force: true });

    splitOk(place, 'LODB_DB_PASSWORD=one\n');

    assert.match(secret(place, GENERATED), HEX_SECRET);
    assert.notEqual(secret(place, GENERATED), drawn);
    assert.equal(revisionOf(place), 'LODB_SECRETS_REVISION=2');
  });

  it('refuses a dotenv that sets the generated password, naming it only', () => {
    const place = workspace();
    splitOk(place, 'LODB_DB_PASSWORD=one\n');
    const drawn = secret(place, GENERATED);

    const run = split(place, `LODB_DB_PASSWORD=one\n${GENERATED}=from-github\n`);

    assert.notEqual(run.status, 0);
    assert.match(run.stderr, new RegExp(`${GENERATED} is generated on the host`));
    assert.doesNotMatch(run.stderr, /from-github/);
    assert.equal(secret(place, GENERATED), drawn);
  });

  it('refuses to run without its two paths', () => {
    const run = spawnSync('bash', [SCRIPT, '.env'], { input: '', encoding: 'utf8' });

    assert.notEqual(run.status, 0);
    assert.match(run.stderr, /usage/);
  });

  it('keeps the folder to root and the files read-only', { skip: !IS_POSIX }, () => {
    const place = workspace();
    writeFileSync(place.env, 'LODB_SECRETS_REVISION=3\n');

    splitOk(place, 'LODB_DB_PASSWORD=secret\n');

    const mode = (path) => statSync(path).mode & 0o777;
    assert.equal(mode(place.secrets), 0o700);
    assert.equal(mode(join(place.secrets, 'LODB_DB_PASSWORD')), 0o444);
    assert.equal(mode(place.env), 0o600);
    assert.equal(revisionOf(place), 'LODB_SECRETS_REVISION=4');
  });
});
