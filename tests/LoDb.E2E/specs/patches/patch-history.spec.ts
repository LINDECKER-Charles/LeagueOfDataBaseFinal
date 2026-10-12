import type { Locator, Page } from '@playwright/test';
import { metaOf, olderVersion } from '../../support/catalog';
import { expect, test } from '../../support/test';

// A link of the history the server renders opens a past patch in the browser, which the stack
// ingests on demand: the router and that ingestion only meet here. The history's links, and
// those of a detail's previous patches, are Vitest's.
const PAGE = '/en/patches';

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
