import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

// Heritage H2: navigation by the header through the router, on the four lists.
const CODEX = ['champions', 'items', 'runes', 'summoners'];
const DESKTOP = { width: 1280, height: 800 };
const MARK = 'lodbSameDocument';

function pathOf(page: Page): string {
  return new URL(page.url()).pathname;
}

// Marks the document: still marked later, the pages changed through the router.
async function markDocument(page: Page): Promise<void> {
  await page.waitForLoadState('networkidle');
  await page.evaluate((mark) => Reflect.set(window, mark, true), MARK);
}

async function isSameDocument(page: Page): Promise<boolean> {
  return page.evaluate((mark) => Reflect.get(window, mark) === true, MARK);
}

test.describe('navigation by the header', { tag: '@readonly' }, () => {
  test('opens each list of the codex from the header menu, through the router', async ({
    page,
    consoleErrors,
  }) => {
    await page.setViewportSize(DESKTOP);
    await page.goto('/en/');
    await markDocument(page);
    const menu = page.locator('header details.switcher--nav');

    for (const resource of CODEX) {
      if (!(await menu.evaluate((details: HTMLDetailsElement) => details.open))) {
        await menu.locator('summary').click();
      }
      const link = menu.locator(`a[href="/en/${resource}"]`);
      await link.click();

      await expect.poll(() => pathOf(page)).toBe(`/en/${resource}`);
      await expect(link).toHaveAttribute('aria-current', 'page');
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    }
    expect(await isSameDocument(page)).toBe(true);
    expect(consoleErrors).toEqual([]);
  });
});
