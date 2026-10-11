import { test as base } from '@playwright/test';
import { fastConnection } from './network';

export { expect } from '@playwright/test';

interface LodbFixtures {
  /**
   * Console errors and uncaught exceptions of the page, in order. A failed hydration or a
   * broken bundle only shows up here, so a spec asserts it is empty.
   */
  readonly consoleErrors: readonly string[];
}

/** Playwright's `test`, plus the fixtures every spec of the suite shares. */
export const test = base.extend<LodbFixtures>({
  // The same connection hints on every machine (support/network.ts), whatever Chromium measured.
  page: async ({ page }, use) => {
    await page.addInitScript(fastConnection);
    await use(page);
  },
  consoleErrors: async ({ page }, use) => {
    const errors: string[] = [];
    page.on('console', (message) => {
      if (message.type() === 'error') {
        errors.push(message.text());
      }
    });
    page.on('pageerror', (error) => errors.push(`${error.name}: ${error.message}`));
    await use(errors);
  },
});
