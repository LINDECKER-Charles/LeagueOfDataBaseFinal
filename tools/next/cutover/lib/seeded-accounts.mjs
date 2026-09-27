// The accounts the rehearsal adds to its copy, as the legacy stack left them: one per hash
// format of tests/fixtures/hashes/hashes.json (made by PHP 8.5 as Symfony makes them), all on
// the same printable password, since the old sign-in is a form.

export const PREFIX = 'cutover_c';
const PASSWORD = 'accents';

const literal = (text) => `'${text.replaceAll("'", "''")}'`;

/** One account per format of the fixtures: username, e-mail, password, format and hash. */
export function seededAccounts(fixtures) {
  const cases = fixtures.cases.filter((entry) => entry.password === PASSWORD);
  return cases.map((entry, index) => {
    const number = String(index + 1).padStart(2, '0');
    return {
      username: `${PREFIX}${number}`,
      email: `${PREFIX}${number}@example.test`,
      password: fixtures.passwords[PASSWORD],
      format: entry.format,
      hash: entry.hash,
    };
  });
}

/**
 * The SQL that (re)creates them with only the columns Doctrine maps on the legacy User entity,
 * verified, so that one of them may issue an API key.
 */
export function seedSql(accounts) {
  const rows = accounts.map(
    (account) => `(${literal(account.email)}, ${literal(account.username)}, '[]',
  ${literal(account.hash)}, false, now(), false, false, true)`,
  );
  return `DELETE FROM users WHERE username LIKE '${PREFIX}%';
INSERT INTO users (email, username, roles, password, is_public_profile, created_at,
  is_supporter, is_banned, is_verified) VALUES
${rows.join(',\n')};`;
}

// The hasher's target (Argon2Passwords): argon2id v19, one lane, at least 19 MiB and 2
// passes. A hash below it is rewritten at the first sign-in to the new stack.
export function atTarget(hash) {
  const match = /^\$argon2id\$v=19\$m=(\d+),t=(\d+),p=1\$/.exec(hash);
  return match !== null && Number(match[1]) >= 19_456 && Number(match[2]) >= 2;
}
