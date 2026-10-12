import type { Page } from '@playwright/test';

// Set on the window: still there later, the page changed through the router, not a load.
const MARK = 'lodbSameDocument';

/**
 * Marks the document once the page has settled, its app driving the links: a later
 * isSameDocument tells a navigation of the router from a load of a new document.
 */
export async function markDocument(page: Page): Promise<void> {
  await page.waitForLoadState('networkidle');
  await page.evaluate((mark) => Reflect.set(window, mark, true), MARK);
}

/** Whether the page still shows the document markDocument marked. */
export async function isSameDocument(page: Page): Promise<boolean> {
  return page.evaluate((mark) => Reflect.get(window, mark) === true, MARK);
}
