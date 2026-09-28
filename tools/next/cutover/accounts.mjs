// Existing accounts across the switch and back (lot 8 exit criterion; L8.2), in three steps
// the rehearsal runs in order on its copy of the database:
//
//   node tools/next/cutover/accounts.mjs seed --state <file> [--postgres lodb-postgres-1]
//        [--database lodb_rehearsal] [--accounts-file <file>]
//   node tools/next/cutover/accounts.mjs new-stack --state <file> --base <new stack>
//        [--postgres …] [--database …]
//   node tools/next/cutover/accounts.mjs legacy-stack --state <file> --base <old site>
//        --api <old go-api>
//
// seed          adds one legacy account per hash format of tests/fixtures/hashes (bcrypt,
//               argon2i, argon2id…), as Doctrine writes them, and records them in --state
//               with those of --accounts-file: accounts of the dump whose password is known,
//               one `<identifier>:<password>` per line (a file, never the command line).
// new-stack     signs every account in to the new stack, checks its hash was rewritten to
//               argon2id (or kept), signs in again, then issues an API key for the first
//               account and asks /v1/usage with it.
// legacy-stack  after the rollback: signs every account in to the old site with its form
//               (the hash the new stack wrote), opens its profile, and asks the old go-api's
//               /v1/usage with the key the new stack issued.
//
// --state holds the accounts and the key's secret: keep it in a temporary directory. The
// database is reached with `docker exec <postgres> psql`. One line per check; exit code 1 on
// any failure, 2 on invalid arguments.
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { SiteClient } from './lib/site-client.mjs';
import { atTarget, seedSql, seededAccounts } from './lib/seeded-accounts.mjs';

const FIXTURES = resolve(dirname(fileURLToPath(import.meta.url)), '../../../tests/fixtures/hashes');
const OK = 200;
const CREATED = 201;
const FOUND = 302;
const UNAUTHORIZED = 401;

const { values, positionals } = parseArgs({
  allowPositionals: true,
  options: {
    state: { type: 'string' },
    base: { type: 'string' },
    api: { type: 'string' },
    postgres: { type: 'string', default: 'lodb-postgres-1' },
    database: { type: 'string', default: 'lodb_rehearsal' },
    'pg-user': { type: 'string', default: 'lodb' },
    'accounts-file': { type: 'string' },
  },
});
const [step] = positionals;
if (values.state === undefined || !['seed', 'new-stack', 'legacy-stack'].includes(step)) {
  console.error('Usage: accounts.mjs (seed|new-stack|legacy-stack) --state <file> [options]');
  process.exit(2);
}

let failures = 0;
function report(ok, label, detail = '') {
  failures += ok ? 0 : 1;
  console.log(`${ok ? 'ok  ' : 'FAIL'} ${label}${detail ? ` (${detail})` : ''}`);
}

function sql(statement) {
  return execFileSync(
    'docker',
    ['exec', '-i', values.postgres, 'psql', '-U', values['pg-user'], '-d', values.database,
      '-v', 'ON_ERROR_STOP=1', '-At', '-F', '\t'],
    { input: statement, encoding: 'utf8' },
  );
}

const quote = (text) => `'${text.replaceAll("'", "''")}'`;

function storedHash(identifier) {
  return sql(
    `SELECT password FROM users WHERE username = ${quote(identifier)} OR email = ${quote(identifier)};`,
  ).trim();
}

const readState = () => JSON.parse(readFileSync(values.state, 'utf8'));
const writeState = (state) => writeFileSync(values.state, `${JSON.stringify(state, null, 2)}\n`, { mode: 0o600 });

function seed() {
  const fixtures = JSON.parse(readFileSync(join(FIXTURES, 'hashes.json'), 'utf8'));
  const seeded = seededAccounts(fixtures);
  sql(seedSql(seeded));
  const lines = values['accounts-file'] === undefined ? [] : readFileSync(values['accounts-file'], 'utf8').split('\n');
  const given = lines.filter((line) => line.includes(':')).map((entry) => {
    const separator = entry.indexOf(':');
    return { username: entry.slice(0, separator), password: entry.slice(separator + 1), format: 'given' };
  });
  const accounts = [...seeded, ...given].map((account) => ({ ...account, hash: storedHash(account.username) }));
  for (const account of accounts) {
    report(account.hash !== '', `${account.username} ${account.format} in ${values.database}`);
  }
  writeState({ accounts });
}

async function signInToNewStack(account) {
  const client = new SiteClient(values.base);
  const response = await client.send('/api/account/login', {
    method: 'POST',
    json: { identifier: account.username, password: account.password, rememberMe: false },
  });
  return { client, status: response.status };
}

async function newStack() {
  const state = readState();
  const signedIn = [];
  for (const account of state.accounts) {
    const first = await signInToNewStack(account);
    const hash = storedHash(account.username);
    const again = await signInToNewStack(account);
    const expected = atTarget(account.hash) ? hash === account.hash : atTarget(hash);
    report(
      first.status === OK && again.status === OK && expected,
      `new stack signs ${account.username} (${account.format}) in`,
      `${first.status}, again ${again.status}, hash ${hash.slice(0, 30)}`,
    );
    account.rewritten = hash;
    if (first.status === OK) {
      signedIn.push(again.client);
    }
  }

  const [owner] = signedIn;
  if (owner === undefined) {
    report(false, 'an account signed in to issue an API key');
    writeState(state);
    return;
  }
  const current = JSON.parse((await owner.send('/api/account/api-key')).text || '{}');
  const issued =
    current.key == null
      ? await owner.send('/api/account/api-key', { method: 'POST', json: { name: 'cutover' } })
      : await owner.send('/api/account/api-key/regenerate', { method: 'POST' });
  const secret = issued.status === OK || issued.status === CREATED ? JSON.parse(issued.text).secret : undefined;
  report(secret !== undefined, 'new stack issues an API key', `${issued.status}`);
  state.key = secret;
  if (secret !== undefined) {
    const usage = await new SiteClient(values.base).send('/v1/usage', {
      headers: { Authorization: `Bearer ${secret}` },
    });
    report(usage.status === OK, 'new stack answers /v1/usage with it', `${usage.status}`);
  }
  writeState(state);
}

async function legacyStack() {
  const state = readState();
  for (const account of state.accounts) {
    const client = new SiteClient(values.base);
    await client.send('/login');
    const login = await client.send('/login', {
      method: 'POST',
      form: { _username: account.username, _password: account.password, _csrf_token: 'csrf-token' },
    });
    const landed = login.status === FOUND && /\/profile$/.test(login.location ?? '');
    const profile = landed ? await client.send('/profile') : { status: 0, text: '' };
    report(
      landed && profile.status === OK && profile.text.includes(account.username),
      `old site signs ${account.username} (${account.format}) in, hash the new stack wrote`,
      `${login.status} ${login.location}, profile ${profile.status}`,
    );
  }

  if (state.key === undefined) {
    report(false, 'a key issued by the new stack to ask the old /v1/usage');
    return;
  }
  const usage = await new SiteClient(values.api).send('/v1/usage', {
    headers: { Authorization: `Bearer ${state.key}` },
  });
  report(usage.status === OK, 'old go-api answers /v1/usage with the key the new stack issued', `${usage.status}`);
  const refused = await new SiteClient(values.api).send('/v1/usage');
  report(refused.status === UNAUTHORIZED, 'old go-api refuses /v1/usage without a key', `${refused.status}`);
}

if (step === 'seed') {
  seed();
} else if (step === 'new-stack') {
  await newStack();
} else {
  await legacyStack();
}
process.exitCode = failures === 0 ? 0 : 1;
