// Holds nginx to the SSR server's `archived` class: a past patch's page from the page store
// gets that Cache-Control from nginx (sites/site.conf), whatever its object says, and the same
// page rendered by the SSR server gets it from CACHE_CONTROL (cache-control.ts). Two copies of
// one constant, in two languages: a change of one alone would give a page two lifetimes, by
// where it came from. Reads files only.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(fileURLToPath(new URL('../../../', import.meta.url)));
const SITE = 'docker/nginx/sites/site.conf';
const CACHE_CONTROL = 'src/LoDb.Web/src/app/core/routing/response/cache-control.ts';
// The value a stored page gets: the map's default, any answer that names its build.
const NGINX_ARCHIVED = /\$lodb_pinned_cache_control \{[^}]*?\bdefault\s+"([^"]+)";/;
const SSR_ARCHIVED = /^\s*archived:\s*'([^']+)',$/m;

async function valueIn(file, pattern) {
  const [, value] = pattern.exec(await readFile(join(ROOT, file), 'utf8')) ?? [];
  assert.ok(value, `${pattern} in ${file}`);
  return value;
}

test("gives a stored page the SSR server's Cache-Control of the archived class", async () => {
  assert.equal(await valueIn(SITE, NGINX_ARCHIVED), await valueIn(CACHE_CONTROL, SSR_ARCHIVED));
});
