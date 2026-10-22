import type { Locator, Page } from '@playwright/test';
import { metaOf, olderVersion } from '../../support/catalog';
import { olderPatchesWithChanges } from '../../support/states';
import { expect, test } from '../../support/test';

// A link of the history the server renders opens a past patch in the browser, which the stack
// ingests on demand, or the page of its changes, which the browser reads from the API's index of
// states: the router, that ingestion and that index only meet here. The history's links, and
// those of a detail's own history, are Vitest's.
const PAGE = '/en/patches';
// The first link of a version's row, when the patch has a page of changes.
const CHANGES = 'Changes';

// The links of a version's row, found by the version its header names.
function linksOf(page: Page, version: string): Locator {
  const header = page.locator('th', {
    hasText: new RegExp(`^\\s*${version.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?:\\s|$)`),
  });
  return page.locator('tbody tr').filter({ has: header }).locator('a');
}

test(
  'opens a past patch from the history in the browser',
  { tag: '@readonly' },
  async ({ page, request, consoleErrors }) => {
    const older = olderVersion(await metaOf(request));
    await page.goto(PAGE);
    await page.waitForLoadState('networkidle');

    await linksOf(page, older).filter({ hasText: 'Runes' }).click();

    await expect(page).toHaveURL((url) => url.pathname === `/en/${older}/runes`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(consoleErrors).toEqual([]);
  },
);

test(
  "opens the page of a past patch's changes from the history in the browser",
  { tag: '@readonly' },
  async ({ page, request, consoleErrors }) => {
    const [version] = olderPatchesWithChanges(await metaOf(request));
    expect(version, 'the index of states must cover the patches before the latest').toBeDefined();
    await page.goto(PAGE);
    await page.waitForLoadState('networkidle');

    await linksOf(page, version ?? '')
      .filter({ hasText: CHANGES })
      .click();

    await expect(page).toHaveURL((url) => url.pathname === `/en/${version}/changes`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(consoleErrors).toEqual([]);
  },
);
