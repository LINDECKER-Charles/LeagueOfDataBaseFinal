import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

// Every value split-env.sh accepts must be the one Compose would have handed the service
// before the split: Compose's own dotenv reader is the reference, not a list of expectations.
const SCRIPT = fileURLToPath(new URL('../split-env.sh', import.meta.url));
const PROJECT = 'lodb-split-env-reference';
// Assignments as they may sit in ENV_STAGING or ENV_PROD, each with a value both readers accept.
const ACCEPTED = [
  'plain',
  'a=b==c;d',
  '   spaced out   ',
  'value # comment',
  ' #starts-with-a-hash',
  'tab\t#is-no-comment',
  'back\\slash',
  "it's",
  'say "hi"',
  "' single $quoted\\ # literal '  # comment",
  '" double # quoted "',
  "''",
  '',
  'unicodé ✓',
];

const hasCompose = spawnSync('docker', ['compose', 'version'], { encoding: 'utf8' }).status === 0;
const workspace = mkdtempSync(join(tmpdir(), 'lodb-split-env-reference-'));
after(() => rmSync(workspace, { recursive: true, force: true }));

const keys = spawnSync('bash', [SCRIPT, '--keys'], { encoding: 'utf8' })
  .stdout.trim()
  .split('\n');

// Compose's reading: a service that takes every secret key from the dotenv.
function composeValues(dotenv) {
  const file = join(workspace, 'compose.yaml');
  const service = ['services:', '  probe:', '    image: busybox', '    environment:'];
  writeFileSync(file, [...service, ...keys.map((key) => `      ${key}:`), ''].join('\n'));
  const run = spawnSync(
    'docker',
    ['compose', '-p', PROJECT, '--env-file', dotenv, '-f', file, 'config', '--format', 'json'],
    { encoding: 'utf8' },
  );
  assert.equal(run.status, 0, run.stderr);
  const rendered = JSON.parse(run.stdout).services.probe.environment;
  // The rendering escapes `$` for a later interpolation.
  return Object.fromEntries(keys.map((key) => [key, rendered[key]?.replaceAll('$$', '$')]));
}

function splitValues(dotenv, batch) {
  const place = mkdtempSync(join(workspace, 'split-'));
  const secrets = join(place, 'secrets');
  const run = spawnSync('bash', [SCRIPT, join(place, '.env'), secrets], {
    input: readFileSync(dotenv),
    encoding: 'utf8',
  });
  assert.equal(run.status, 0, run.stderr);
  return Object.fromEntries(
    batch.map(([key]) => [key, readFileSync(join(secrets, key), 'utf8')]),
  );
}

describe('split-env.sh against Compose', { skip: !hasCompose && 'no docker compose' }, () => {
  for (let start = 0; start < ACCEPTED.length; start += keys.length) {
    const batch = ACCEPTED.slice(start, start + keys.length).map((value, index) => [
      keys[index],
      value,
    ]);

    it(`reads values ${start + 1} to ${start + batch.length} as Compose does`, () => {
      const dotenv = join(workspace, `batch-${start}.env`);
      writeFileSync(dotenv, batch.map(([key, value]) => `${key}=${value}`).join('\n'));

      const expected = composeValues(dotenv);
      const actual = splitValues(dotenv, batch);

      for (const [key, value] of batch) {
        assert.equal(actual[key], expected[key], `${key}=${value}`);
      }
    });
  }
});
