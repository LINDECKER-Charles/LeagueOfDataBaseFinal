// Snapshot of the old-stack schema, taken read-only from its Postgres container. The
// capture loads this snapshot into an empty private database, so replays never need
// (nor touch) the developer database once the snapshot is committed.
import fs from 'node:fs';
import path from 'node:path';
import { docker } from './docker.mjs';
import { postgres, seedDir } from './settings.mjs';

// pg_dump 17 wraps its output in \restrict / \unrestrict with a random key per run.
const restrictLine = /^\\(un)?restrict\b/;

/** Removes the per-run noise of a pg_dump so the snapshot is stable. */
export function stableDump(dump) {
  const lines = dump.split('\n').filter((line) => !restrictLine.test(line));
  return `${lines.join('\n').trim()}\n`;
}

/** Dumps the schema of `container` into seed/schema.sql. */
export function refreshSchema(container) {
  const result = docker(['exec', container, 'pg_dump', '--schema-only', '--no-owner',
    '--no-privileges', '-U', postgres.user, '-d', postgres.database]);
  const target = path.join(seedDir, 'schema.sql');
  fs.writeFileSync(target, stableDump(result.stdout));
  return target;
}
