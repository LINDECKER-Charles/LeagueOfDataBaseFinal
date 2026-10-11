// The site's declaration to Umami against its copies: the website id is a literal of
// compose.umami.yaml (labels of nginx, the dashboard's import, the API's pull) and of the
// front (core/analytics/audience/audience-site.ts), chosen once and never generated. A copy
// that drifts sends the views to no site, or the dashboard to another one. Reads the files
// only: no Docker needed (tools/compose/test renders the overlay through Compose).
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(fileURLToPath(new URL('../../../', import.meta.url)));
const OVERLAY = 'compose.umami.yaml';
const FRONT = 'src/LoDb.Web/src/app/core/analytics/audience/audience-site.ts';
// What Umami 3.4.0 accepts and the reconciler checks, narrowed to a version 4 UUID.
const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const SERVED_WITHOUT_UMAMI = ['compose.yaml', 'compose.deploy.yaml', 'compose.staging.yaml'];

const read = (file) => readFile(join(ROOT, file), 'utf8');

function only(text, pattern, what) {
  const found = [...text.matchAll(pattern)];
  assert.equal(found.length, 1, `${what}: expected once, found ${found.length}`);
  return found[0][1];
}

describe('the declaration to Umami', async () => {
  const overlay = await read(OVERLAY);
  const front = await read(FRONT);
  const website = only(overlay, /^\s*umami\.website: &website (\S+)$/gm, 'umami.website');

  test('names a version 4 UUID as the website', () => {
    assert.match(website, UUID_V4);
  });

  test('gives the front, the dashboard and the pull the same website id', () => {
    assert.equal(only(front, /websiteId: '([^']+)'/g, 'front websiteId'), website);
    assert.equal(
      only(overlay, /^\s*caddy_2\.import: umami-tableau (\S+)$/gm, 'umami-tableau'),
      website,
    );
    assert.equal(
      only(overlay, /^\s*LoDb__Audience__WebsiteId: (\S+)$/gm, 'pull website'),
      '*website',
    );
  });

  test('declares the domain the front measures', () => {
    const domain = only(overlay, /^\s*umami\.domain: (\S+)$/gm, 'umami.domain');
    assert.equal(only(front, /hostname: '([^']+)'/g, 'front hostname'), domain);
  });

  test('imports the tracker into the site, never the API subdomain nor a global block', () => {
    assert.equal(only(overlay, /^\s*caddy_0\.import: (\S+)$/gm, 'caddy_0.import'), 'umami');
    assert.doesNotMatch(overlay, /^\s*caddy\.import/m);
    assert.doesNotMatch(overlay, /^\s*caddy_1\.import/m);
  });

  test('stays out of every file that staging also reads', async () => {
    for (const file of SERVED_WITHOUT_UMAMI) {
      const text = await read(file);
      assert.doesNotMatch(text, /^\s*umami\./m, file);
      assert.doesNotMatch(text, /import: umami/, file);
      assert.equal(text.includes(website), false, file);
    }
  });
});
