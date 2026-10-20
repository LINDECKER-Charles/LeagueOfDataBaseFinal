import { metaOf, olderVersion } from '../../support/catalog';
import { hydrated } from '../../support/hydration';
import { recordViolations } from '../../support/policy-violations';
import { execInStack } from '../../support/stack';
import { expect, test } from '../../support/test';

// A past patch's page that the pass wrote to the stack's simulated store (pages-store), read
// back by nginx, then hydrated in a browser under the policy its object carried (ADR 0011): the
// pass, the store, nginx and the bundles of the build that rendered the page only meet here.
// What nginx does with each answer of the store is tools/nginx's; what the pass writes, and
// when, the API's tests'.

// Two patches back: the other specs open the previous one, whose pages nginx may keep already.
const STEPS_BACK = 2;
const LOCALE = 'en';
// The smallest type of page: its list and a few spells.
const TYPE = 'summoners';
// A spell of every patch. A detail page rather than the list: a list rendered while its
// images are still on their way is not kept, nor stored (docs/architecture/pieges.md).
const PAGE = 'summoners/SummonerFlash';
// The unit ingests its catalog, then may wait for every server to count its patch among the
// older ones: up to three minutes after the latest one's promotion, on a stack just started.
const UNIT_WAIT_SECONDS = 240;
const SECOND_MS = 1_000;
const COMMAND_MARGIN_MS = 60_000;
const COMMAND_TIMEOUT_MS = UNIT_WAIT_SECONDS * SECOND_MS + COMMAND_MARGIN_MS;
const BROWSING_MS = 60_000;
// What the command prints of its unit (src/LoDb.Api/Cli/Prerender/PrerenderUnitCommand.cs).
const UNIT_SUMMARY = / (\d+) written, \d+ skipped, 0 failed of \d+,/;
// The `archived` class (src/LoDb.Web/src/app/core/routing/response/cache-control.ts), which
// nginx gives every answer of the store.
const ARCHIVED = 'public, max-age=3600, s-maxage=31536000';
// The hot cache in front of the store (snippets/archive-cache.conf), one file per page.
const ARCHIVE_CACHE = '/var/cache/nginx/archive';

// Writes the pages of one type of the (version, locale) unit with the one-unit command, run in
// the API container; resolves to how many it wrote.
function writeUnit(version: string): number {
  const command = ['dotnet', 'LoDb.Api.dll', 'prerender', 'unit', '--version', version];
  const options = ['--locale', LOCALE, '--type', TYPE, '--wait', String(UNIT_WAIT_SECONDS)];
  const output = execInStack('api', [...command, ...options], { timeoutMs: COMMAND_TIMEOUT_MS });
  const written = UNIT_SUMMARY.exec(output)?.[1];
  expect(written, output).toBeDefined();
  return Number(written);
}

// A copy that nginx kept on an earlier run would answer before the store: its file goes, which
// nginx then takes for a miss. The key is written in each file's header, the body compressed.
function forgetKeptCopies(path: string): void {
  const script = `grep -rlF -e "$0" ${ARCHIVE_CACHE} | xargs -r rm -f`;
  execInStack('nginx', ['sh', '-c', script, path]);
}

test.describe('past patches served from the page store', () => {
  test.setTimeout(COMMAND_TIMEOUT_MS + BROWSING_MS);

  test('serves the page the pass stored, which hydrates under its own policy', async ({
    page,
    request,
    consoleErrors,
  }) => {
    const version = olderVersion(await metaOf(request), STEPS_BACK);
    const path = `/${LOCALE}/${version}/${PAGE}`;

    await test.step('write the unit with the command', () => {
      expect(writeUnit(version)).toBeGreaterThan(0);
      forgetKeptCopies(path);
    });

    await test.step('answer from the store, with the headers of its object', async () => {
      const response = await request.get(path);
      const headers = response.headers();

      expect(response.status()).toBe(200);
      expect(headers['x-lodb-source']).toBe('store');
      expect(headers['content-type']).toContain('text/html');
      expect(headers['cache-control']).toBe(ARCHIVED);
      expect(headers['content-security-policy']).toContain('script-src');
      expect(Object.keys(headers).filter((name) => name.startsWith('x-amz-'))).toEqual([]);
    });

    // Its copy, now kept by nginx: the headers come back from the object's metadata on a HIT.
    await test.step('hydrate the kept copy without a violation', async () => {
      const violations = await recordViolations(page);

      const response = await page.goto(path);
      await hydrated(page.locator('lodb-context-switcher'));

      expect(response?.headers()['x-lodb-source']).toBe('cache');
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      expect(await violations()).toEqual([]);
      expect(consoleErrors).toEqual([]);
    });
  });
});
