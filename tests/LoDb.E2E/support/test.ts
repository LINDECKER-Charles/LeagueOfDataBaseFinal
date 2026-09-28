import { test as base } from '@playwright/test';

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
