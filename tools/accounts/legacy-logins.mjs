// Exit criterion of lot 4 (plan, §8): accounts whose hash the legacy stack wrote sign in to
// the new one, and the hash rewritten at the sign-in is read by PHP's password_verify.
//
//   node tools/accounts/legacy-logins.mjs [--base http://localhost:18080]
//        [--postgres lodb-dev-postgres-1] [--keep]
//
// For every case of tests/fixtures/hashes/hashes.json (bcrypt and argon2 hashes made by
// PHP 8.5 as Symfony makes them), the script inserts a user in SQL with only the columns
// Doctrine writes, signs in through nginx, reads the rewritten hash back, signs in again
// with it, and checks a wrong password is refused. It then hands every rewritten hash to
// tests/fixtures/hashes/verify.php in a throwaway php:8.5-cli container without network.
// The users are named jalon4_cNN and deleted at the end unless --keep is given.
//
// Needs a started stack and Docker. Prints one line per case; exit code 1 on any failure.
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';

const FIXTURES = resolve(dirname(fileURLToPath(import.meta.url)), '../../tests/fixtures/hashes');
const PHP_IMAGE = 'php:8.5-cli';
const PREFIX = 'jalon4_c';

const { values } = parseArgs({
  options: {
    base: { type: 'string', default: 'http://localhost:18080' },
    postgres: { type: 'string', default: 'lodb-dev-postgres-1' },
    keep: { type: 'boolean', default: false },
  },
});

const fixtures = JSON.parse(readFileSync(join(FIXTURES, 'hashes.json'), 'utf8'));
const cases = fixtures.cases.map((entry, index) => {
  const number = String(index + 1).padStart(2, '0');
  return {
    ...entry,
    username: `${PREFIX}${number}`,
    email: `${PREFIX}${number}@example.test`,
    plain: fixtures.passwords[entry.password],
  };
});

function sql(statement) {
  return execFileSync(
    'docker',
    [
      'exec',
      '-i',
      values.postgres,
      'psql',
      '-U',
      'lodb',
      '-d',
      'lodb',
      '-v',
      'ON_ERROR_STOP=1',
      '-At',
      '-F',
      '\t',
    ],
    { input: statement, encoding: 'utf8' },
  );
}

const literal = (text) => `'${text.replaceAll("'", "''")}'`;

// The hasher's target (Argon2Passwords): argon2id v19, one lane, at least 19 MiB and 2
// passes. A hash at or above it is kept; any other is rewritten at exactly the target.
const TARGET = '$argon2id$v=19$m=19456,t=2,p=1$';
function atTarget(hash) {
  const match = /^\$argon2id\$v=19\$m=(\d+),t=(\d+),p=1\$/.exec(hash);
  return match !== null && Number(match[1]) >= 19_456 && Number(match[2]) >= 2;
}

async function signIn(identifier, password) {
  const response = await fetch(`${values.base}/api/account/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Origin: values.base },
    body: JSON.stringify({ identifier, password, rememberMe: false }),
  });
  const body = await response.text();
  return { status: response.status, code: response.ok ? '' : JSON.parse(body || '{}').code };
}

function storedHashes() {
  const rows = sql(`SELECT username, password FROM users WHERE username LIKE '${PREFIX}%';`);
  return new Map(
    rows
      .trim()
      .split('\n')
      .filter(Boolean)
      .map((row) => row.split('\t')),
  );
}

function phpVerify(entries) {
  const work = mkdtempSync(join(tmpdir(), 'lodb-lot4-'));
  try {
    writeFileSync(join(work, 'cases.json'), JSON.stringify(entries));
    const output = execFileSync(
      'docker',
      [
        'run',
        '--rm',
        '--network',
        'none',
        '-v',
        `${FIXTURES}:/fixtures:ro`,
        '-v',
        `${work}:/work:ro`,
        PHP_IMAGE,
        'php',
        '/fixtures/verify.php',
        '/work/cases.json',
      ],
      { encoding: 'utf8' },
    );
    return JSON.parse(output);
  } finally {
    rmSync(work, { recursive: true, force: true });
  }
}

const cleanup = `DELETE FROM users WHERE username LIKE '${PREFIX}%';`;
sql(cleanup);
// The columns Doctrine maps on the legacy User entity: none of those Identity added.
sql(
  cases
    .map(
      (entry) => `INSERT INTO users (email, username, roles, password,
  is_public_profile, created_at, is_supporter, is_banned, is_verified) VALUES
  (${literal(entry.email)}, ${literal(entry.username)}, '[]',
  ${literal(entry.hash)}, false, now(), false, false, true);`,
    )
    .join('\n'),
);

const results = [];
for (const entry of cases) {
  const first = await signIn(entry.username, entry.plain);
  results.push({ entry, first });
}
const rewritten = storedHashes();
for (const result of results) {
  const { entry } = result;
  result.hash = rewritten.get(entry.username) ?? '';
  result.again = await signIn(entry.email, entry.plain);
  result.wrong = await signIn(entry.username, `${entry.plain}x`);
}
const php = phpVerify(results.map(({ entry, hash }) => ({ password: entry.plain, hash })));

let failures = 0;
results.forEach((result, index) => {
  const { entry, first, again, wrong, hash } = result;
  const checks = {
    login: first.status === 200,
    [atTarget(entry.hash) ? 'kept' : 'rewritten']: atTarget(entry.hash)
      ? hash === entry.hash
      : hash.startsWith(TARGET),
    relogin: again.status === 200,
    refused: wrong.status === 401,
    password_verify: php[index]?.password_verify === true,
    symfony: php[index]?.symfony === true,
  };
  const failed = Object.entries(checks)
    .filter(([, ok]) => !ok)
    .map(([name]) => name);
  failures += failed.length > 0 ? 1 : 0;
  const detail =
    failed.length === 0
      ? 'ok'
      : `FAIL ${failed.join(',')} (login ${first.status} ${first.code}, again ${again.status}, ` +
        `wrong ${wrong.status}, hash ${hash.slice(0, 32)})`;
  console.log(`${entry.username} ${entry.format.padEnd(16)} ${entry.password.padEnd(8)} ${detail}`);
});

if (!values.keep) {
  sql(cleanup);
}
console.log(`${cases.length - failures}/${cases.length} cases pass`);
process.exitCode = failures === 0 ? 0 : 1;
