import type { APIRequestContext, Page } from '@playwright/test';
import { type CatalogMeta, metaOf, olderVersion } from '../../support/catalog';
import { expect, test } from '../../support/test';

// One entry per resource, by id: its canonical path comes from the API.
const ENTRIES = [
  ['champions', 'Annie'],
  ['items', '1036'],
  ['runes', '8100'],
  ['summoners', 'SummonerFlash'],
] as const;
const PREVIOUS_COUNT = 6;

async function canonicalPathOf(
  request: APIRequestContext,
  version: string,
  [resource, id]: (typeof ENTRIES)[number],
): Promise<string> {
  const response = await request.get(`/api/catalog/${version}/en_US/${resource}/${id}`);
  expect(response.status(), `${resource}/${id} in ${version}`).toBe(200);
  return ((await response.json()) as { canonicalPath: string }).canonicalPath;
}

// The versions listed before `version`, which the block links.
function previousOf(meta: CatalogMeta, version: string): readonly string[] {
  const index = meta.versions.indexOf(version);
  return meta.versions.slice(index + 1, index + 1 + PREVIOUS_COUNT);
}

async function blockHrefs(page: Page): Promise<(string | null)[]> {
  return page
    .locator('lodb-previous-patches a')
    .evaluateAll((links) => links.map((link) => link.getAttribute('href')));
}

test.describe(
  'previous patches of a detail page, as crawlers read it',
  { tag: '@readonly' },
  () => {
    test.use({ javaScriptEnabled: false });

    for (const entry of ENTRIES) {
      test(`links the ${entry[0]} entry in the patches before the latest`, async ({
        page,
        request,
      }) => {
        const meta = await metaOf(request);
        const path = await canonicalPathOf(request, meta.latest, entry);

        await page.goto(`/en/${path}`);

        await expect(page.locator('lodb-previous-patches h2')).toHaveText('In previous patches');
        expect(await blockHrefs(page)).toEqual(
          previousOf(meta, meta.latest).map((version) => `/en/${version}/${path}`),
        );
      });
    }

    test('links, from an archived page, the patches before its own', async ({ page, request }) => {
      const meta = await metaOf(request);
      const older = olderVersion(meta);
      const path = await canonicalPathOf(request, older, ENTRIES[0]);

      await page.goto(`/en/${older}/${path}`);

      expect(await blockHrefs(page)).toEqual(
        previousOf(meta, older).map((version) => `/en/${version}/${path}`),
      );
    });
  },
);

test(
  'opens the entry in a previous patch',
  { tag: '@readonly' },
  async ({ page, request, consoleErrors }) => {
    const meta = await metaOf(request);
    const older = olderVersion(meta);
    const path = await canonicalPathOf(request, meta.latest, ENTRIES[0]);
    await page.goto(`/en/${path}`);
    await page.waitForLoadState('networkidle');

    await page.locator('lodb-previous-patches').getByRole('link', { name: older }).click();

    await expect(page).toHaveURL((url) => url.pathname === `/en/${older}/${path}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(consoleErrors).toEqual([]);
  },
);
