import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';
import { after, before, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

// The deploy overlay, rendered by Compose from each environment's dotenv template, against
// the secret keys of split-env.sh: every secret is a mounted file, none an environment value.
const ROOT = fileURLToPath(new URL('../../..', import.meta.url));
const SCRIPT = join(ROOT, 'tools', 'deploy', 'split-env.sh');
const TEMPLATES = ['.env.staging.example', '.env.prod.example'];
const DATABASE_PASSWORD = 'LODB_DB_PASSWORD';
const APP_DATABASE_PASSWORD = 'LODB_APP_DB_PASSWORD';
const DATABASE_PASSWORD_SETTING = 'LoDb__Database__Password';
const APP_ROLE_PASSWORD_SETTING = 'LoDb__Database__AppRole__Password';
// The off-host backups' role: migrate sets its password, the API never reads it.
const BACKUP_ROLE_PASSWORD = 'LoDb__Database__BackupRole__Password';
const APP_ROLE = 'Username=lodb_app;';
const POSTGRES_PASSWORD_FILE = '/run/secrets/postgres-password';
const REVISION_LABEL = 'league-of-data-base.secrets-revision';
// Written by the deploy job on the host (configuration.md); Compose requires it.
const HOST_ADDITIONS = {
  LODB_EDGE_CIDR: '172.18.0.0/16',
  LODB_STACK_SUBNET: '10.231.1.0/24',
  LODB_EDGE_SECRET: '5e'.repeat(32),
};

const hasCompose = spawnSync('docker', ['compose', 'version'], { encoding: 'utf8' }).status === 0;
const workspace = mkdtempSync(join(tmpdir(), 'lodb-compose-secrets-'));
after(() => rmSync(workspace, { recursive: true, force: true }));

// --keys: the secrets of the dotenv; --generated: those the job draws on the host.
const listed = (option) =>
  spawnSync('bash', [SCRIPT, option], { encoding: 'utf8' }).stdout.trim().split('\n');
const marker = (key) => `marker-value-of-${key.toLowerCase()}`;
const targetName = (secret) => basename(secret.target.replaceAll('\\', '/'));

// The template, every secret line replaced by a value the rendering must never show.
function dotenvWithMarkers(template, keys) {
  const assigns = new RegExp(`^\\s*(export\\s+)?(${keys.join('|')})\\s*=`);
  const kept = readFileSync(join(ROOT, template), 'utf8')
    .split(/\r?\n/)
    .filter((line) => !assigns.test(line));
  return [...kept, ...keys.map((key) => `${key}='${marker(key)}'`)].join('\n');
}

function render(template, keys) {
  const dotenv = join(workspace, template);
  writeFileSync(dotenv, dotenvWithMarkers(template, keys));
  const run = spawnSync(
    'docker',
    [
      'compose',
      '--env-file',
      dotenv,
      '-f',
      'compose.yaml',
      '-f',
      'compose.deploy.yaml',
      'config',
      '--format',
      'json',
    ],
    { cwd: ROOT, encoding: 'utf8', env: { ...process.env, ...HOST_ADDITIONS } },
  );
  assert.equal(run.status, 0, run.stderr);
  return { text: run.stdout, config: JSON.parse(run.stdout) };
}

for (const template of TEMPLATES) {
  describe(
    `compose.deploy.yaml with ${template}`,
    { skip: !hasCompose && 'no docker compose' },
    () => {
      let keys;
      let generated;
      let rendered;
      before(() => {
        keys = listed('--keys');
        generated = listed('--generated');
        rendered = render(template, [...keys, ...generated]);
      });

      it('declares one file of .deploy/secrets per secret key, and no other', () => {
        const files = Object.entries(rendered.config.secrets)
          .filter(([, secret]) => /[\\/]\.deploy[\\/]secrets[\\/]/.test(secret.file))
          .map(([name, secret]) => [name, basename(secret.file.replaceAll('\\', '/'))]);

        assert.deepEqual(files.map(([name]) => name).sort(), [...keys, ...generated].sort());
        for (const [name, file] of files) {
          assert.equal(file, name);
        }
      });

      it('never renders a secret value, even from a .env that still holds it', () => {
        for (const key of [...keys, ...generated]) {
          assert.equal(rendered.text.includes(marker(key)), false, key);
        }
      });

      it('mounts each setting of the API under its key, its own role password included', () => {
        const api = rendered.config.services.api;
        const targets = new Map(api.secrets.map((secret) => [secret.source, targetName(secret)]));

        const notTheApis = [DATABASE_PASSWORD, BACKUP_ROLE_PASSWORD];
        for (const key of keys.filter((name) => !notTheApis.includes(name))) {
          assert.equal(targets.get(key), key);
          assert.equal(key in api.environment, false, key);
        }
        for (const key of notTheApis) {
          assert.equal(targets.has(key), false, key);
          assert.equal(key in api.environment, false, key);
        }
        assert.equal(targets.get(APP_DATABASE_PASSWORD), DATABASE_PASSWORD_SETTING);
        assert.ok(api.environment.ConnectionStrings__LoDb.includes(APP_ROLE));
        assert.match(api.labels[REVISION_LABEL], /^\d+$/);
      });

      it('gives migrate the passwords of the roles, postgres its own, as files only', () => {
        const { migrate, postgres, api } = rendered.config.services;

        assert.deepEqual(
          migrate.secrets.map((secret) => [secret.source, targetName(secret)]),
          [
            [DATABASE_PASSWORD, DATABASE_PASSWORD_SETTING],
            [APP_DATABASE_PASSWORD, APP_ROLE_PASSWORD_SETTING],
            [BACKUP_ROLE_PASSWORD, BACKUP_ROLE_PASSWORD],
          ],
        );
        for (const setting of [APP_ROLE_PASSWORD_SETTING, BACKUP_ROLE_PASSWORD]) {
          assert.equal(setting in migrate.environment, false, setting);
        }
        assert.equal(migrate.environment.ConnectionStrings__LoDb.includes(APP_ROLE), false);
        for (const service of [api, migrate]) {
          assert.doesNotMatch(service.environment.ConnectionStrings__LoDb, /password/i);
        }
        assert.equal('POSTGRES_PASSWORD' in postgres.environment, false);
        assert.equal(postgres.environment.POSTGRES_PASSWORD_FILE, POSTGRES_PASSWORD_FILE);
        assert.deepEqual(
          postgres.secrets.map((secret) => [secret.source, targetName(secret)]),
          [[DATABASE_PASSWORD, basename(POSTGRES_PASSWORD_FILE)]],
        );
      });
    },
  );
}
