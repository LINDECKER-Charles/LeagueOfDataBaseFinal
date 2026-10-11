import type { APIRequestContext } from '@playwright/test';
import { metaOf, olderVersion } from '../../support/catalog';
import { expect, test } from '../../support/test';

// A champion every patch carries: its canonical path comes from the API.
const CHAMPION = 'Annie';

async function canonicalPathOf(request: APIRequestContext, version: string): Promise<string> {
  const response = await request.get(`/api/catalog/${version}/en_US/champions/${CHAMPION}`);
  expect(response.status(), `${CHAMPION} in ${version}`).toBe(200);
  return ((await response.json()) as { canonicalPath: string }).canonicalPath;
}

test(
  'opens the entry in a previous patch',
  { tag: '@readonly' },
  async ({ page, request, consoleErrors }) => {
    const meta = await metaOf(request);
    const older = olderVersion(meta);
    const path = await canonicalPathOf(request, meta.latest);
    await page.goto(`/en/${path}`);
    await page.waitForLoadState('networkidle');

    await page.locator('lodb-previous-patches').getByRole('link', { name: older }).click();

    await expect(page).toHaveURL((url) => url.pathname === `/en/${older}/${path}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(consoleErrors).toEqual([]);
  },
);
