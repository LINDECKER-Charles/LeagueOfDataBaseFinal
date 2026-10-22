// Holds nginx to the SSR server's classes of a past patch's page: a page from the page store
// gets from nginx (sites/site.conf) the Cache-Control of its object's class, `revisable` for an
// object of that class and `archived` otherwise, and the same page rendered by the SSR server
// gets it from CACHE_CONTROL (cache-control.ts). Copies of one constant, in two languages, the
// revisable one twice in nginx (the value matched, the value given): a change of one alone would
// give a page two lifetimes, by where it came from, or leave the open state's pages a year in
// every cache. Reads files only.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(fileURLToPath(new URL('../../../', import.meta.url)));
const SITE = 'docker/nginx/sites/site.conf';
const CACHE_CONTROL = 'src/LoDb.Web/src/app/core/routing/response/cache-control.ts';
// The map that gives a stored page its Cache-Control, up to its closing brace.
const NGINX_MAP = /\$lodb_pinned_cache_control \{([^}]*)\}/;
// Its default, any answer that names its build: the archived class.
const NGINX_ARCHIVED = /\bdefault\s+"([^"]+)";/;
// The revisable branch: an object of a build whose Cache-Control is exactly the value matched,
// and the value given.
const NGINX_REVISABLE = /"~\^\[\^\|\]\+\\\|([^"]+)\\z"\s+"([^"]+)";/;
const SSR_ARCHIVED = /^\s*archived:\s*'([^']+)',$/m;
const SSR_REVISABLE = /^\s*revisable:\s*'([^']+)',$/m;

async function read(file) {
  return readFile(join(ROOT, file), 'utf8');
}

function match(text, pattern, where) {
  const found = pattern.exec(text);
  assert.ok(found, `${pattern} in ${where}`);
  return found;
}

async function nginxMap() {
  return match(await read(SITE), NGINX_MAP, SITE)[1];
}

test("gives a stored page the SSR server's Cache-Control of the archived class", async () => {
  const [, nginx] = match(await nginxMap(), NGINX_ARCHIVED, SITE);
  const [, ssr] = match(await read(CACHE_CONTROL), SSR_ARCHIVED, CACHE_CONTROL);
  assert.equal(nginx, ssr);
});

test("gives a revisable stored page the SSR server's Cache-Control of its class", async () => {
  const [, matched, given] = match(await nginxMap(), NGINX_REVISABLE, SITE);
  const [, ssr] = match(await read(CACHE_CONTROL), SSR_REVISABLE, CACHE_CONTROL);
  assert.deepEqual([matched, given], [ssr, ssr]);
});
