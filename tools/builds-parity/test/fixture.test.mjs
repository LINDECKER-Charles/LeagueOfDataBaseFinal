import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';

const fixture = readFileSync(new URL('../fixture.sql', import.meta.url), 'utf8');
const tokens = JSON.parse(readFileSync(new URL('../tokens.json', import.meta.url), 'utf8'));
// A build row of fixture.sql starts with its token, language and visibility.
const ROW = /\('(feedbeef[0-9a-f]{16})', '([a-z]{2}_[A-Z]{2})', (true|false),/g;
const rows = [...fixture.matchAll(ROW)].map(([, token, language, visibility]) => ({
  token,
  language,
  public: visibility === 'true',
}));

test('lists every build of the fixture, with its language and visibility', () => {
  const listed = tokens.filter((build) => build.public !== null);
  assert.deepEqual(
    listed.map(({ token, language, public: visible }) => ({ token, language, public: visible })),
    rows,
  );
});

test('holds tokens the share route accepts, and one no build holds', () => {
  for (const { token } of tokens) assert.match(token, /^[a-f0-9]{24}$/);
  const unknown = tokens.filter((build) => build.public === null);
  assert.equal(unknown.length, 1);
  assert.ok(!fixture.includes(unknown[0].token));
});

test('covers both visibilities, the four modes, both patches and the three ghosts', () => {
  assert.ok(rows.some((row) => row.public) && rows.some((row) => !row.public));
  for (const mode of ["'sr'", "'aram'", "'arena'", "'nexus_blitz'"]) {
    assert.ok(fixture.includes(`, ${mode}, :'`), mode);
  }
  assert.ok(fixture.includes(":'latest'") && fixture.includes(":'older'"));
  for (const ghost of ["'ParityGhost'", '"999999"', '[9999,']) assert.ok(fixture.includes(ghost));
});
