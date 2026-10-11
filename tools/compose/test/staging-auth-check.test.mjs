// Runs the deploy job's own check of the staging login (check_staging_auth in
// .github/workflows/_deploy.yml) against the .env lines it must accept or refuse. The name
// becomes a segment of a label key of the shared edge, which reads a `.` as nesting (the site
// is dropped) and `_<digits>` / `<digits>_` as ordering marks (the name is cut): it must be
// refused before the deploy changes anything.
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
const WORKFLOW = join(ROOT, '.github', 'workflows', '_deploy.yml');
const FUNCTION_START = 'check_staging_auth() {';
const HASH = '$2a$14$jM2G4xK0ZDa8W6PF31H9BOKCv81PEwu/7g692HVRaN72tRoi1jH52';
const QUOTED = `'${HASH}'`;
const DOUBLED = HASH.replaceAll('$', () => '$$');

const hasBash = await run('bash', ['-c', 'true']).then(() => true, () => false);

// The function as the job defines it, without the indentation of the workflow.
async function checkFunction() {
  const lines = (await readFile(WORKFLOW, 'utf8')).split('\n');
  const start = lines.findIndex((line) => line.trim() === FUNCTION_START);
  assert.notEqual(start, -1, `${FUNCTION_START} is in the deploy job`);
  const indent = lines[start].indexOf(FUNCTION_START);
  const end = lines.findIndex((line, index) => index > start && line === `${' '.repeat(indent)}}`);
  return lines.slice(start, end + 1).map((line) => line.slice(indent)).join('\n');
}

describe('the deploy job check of the staging login', {
  skip: hasBash ? false : 'bash is not available',
}, () => {
  let folder;
  let script;

  before(async () => {
    folder = await mkdtemp(join(tmpdir(), 'lodb-staging-auth-'));
    script = [
      'set -euo pipefail',
      'fail() { echo "$*" >&2; exit 1; }',
      await checkFunction(),
      'check_staging_auth "$1" "$2"',
    ].join('\n');
  });

  after(() => folder && rm(folder, { recursive: true, force: true }));

  // The user and the hash as env_value reads them, the hash line as the .env holds it.
  async function check(user, hashLine) {
    await writeFile(join(folder, '.env'), `LODB_STAGING_BASIC_AUTH_HASH=${hashLine}\n`);
    const hash = hashLine.replaceAll(/["']/g, '');
    return run('bash', ['-c', script, 'check', user, hash], { cwd: folder });
  }

  test('accepts a plain name with a single-quoted or doubled bcrypt hash', async () => {
    await check('tester', QUOTED);
    await check('Staging-Team-2', DOUBLED);
  });

  for (const user of ['john.doe', 'bob_2', '1_admin', 'test_user', 'a b']) {
    test(`refuses the name ${JSON.stringify(user)}`, async () => {
      await assert.rejects(check(user, QUOTED), /LODB_STAGING_BASIC_AUTH_USER: letters/);
    });
  }

  for (const hashLine of [`"${HASH}"`, HASH, `'${HASH.slice(0, -1)}'`]) {
    test(`refuses the hash line ${hashLine.slice(0, 12)}…`, async () => {
      await assert.rejects(check('tester', hashLine), /must be a single-quoted bcrypt hash/);
    });
  }

  test('refuses a name without its hash', async () => {
    await assert.rejects(check('tester', ''), /set together, or neither/);
  });
});
