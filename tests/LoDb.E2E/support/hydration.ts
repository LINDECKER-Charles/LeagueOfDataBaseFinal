import { expect, type Locator, type Page } from '@playwright/test';

/**
 * Waits for the app to hydrate the page the server rendered, and to settle once: `scope`,
 * a component of the page or the page itself, is then driven by the app.
 *
 * The server marks each element with a listener `jsaction`, for the replay of early events.
 * Angular removes every mark of the page's eager content at once, when the hydrated app is
 * first stable: its initial navigation (whose end folds the header menus) and its first
 * requests are over. Until then, a field of the server's page may change under the spec, or
 * a form be sent without the app. A component the browser rendered itself carries no mark:
 * the wait ends at once. `scope` must hold an element with a listener, or nothing is waited.
 */
export async function hydrated(scope: Page | Locator): Promise<void> {
  await expect(scope.locator('[jsaction]')).toHaveCount(0);
}
