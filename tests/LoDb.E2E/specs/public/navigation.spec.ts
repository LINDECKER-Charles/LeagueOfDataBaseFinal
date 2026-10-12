import type { Page } from '@playwright/test';
import { isSameDocument, markDocument } from '../../support/same-document';
import { expect, test } from '../../support/test';

// Heritage H2: navigation by the header through the router, on the four lists. Only a page the
// server rendered, then hydrated in a real browser, shows its links leaving the document in
// place; the header's links are Vitest's.
const CODEX = ['champions', 'items', 'runes', 'summoners'];
const DESKTOP = { width: 1280, height: 800 };

function pathOf(page: Page): string {
  return new URL(page.url()).pathname;
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
