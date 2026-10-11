import { recordViolations } from '../../support/policy-violations';
import { expect, test } from '../../support/test';

// One page per way of rendering one: by the server on request, prerendered at build time, and
// in the browser alone (the account's pages). The guard at each upgrade of Angular and
// beasties (docs/architecture/pieges.md); the policy itself is the SSR server's unit tests'.
const PAGES = ['/fr/champions/Annie', '/en/about', '/en/account/login'];

test.describe('the content security policy of the pages', { tag: '@readonly' }, () => {
  for (const path of PAGES) {
    test(`lets ${path} hydrate without a violation, Trusted Types included`, async ({
      page,
      consoleErrors,
    }) => {
      const violations = await recordViolations(page);

      const response = await page.goto(path);
      await page.waitForLoadState('networkidle');

      const headers = response?.headers() ?? {};
      expect(headers['content-security-policy']).not.toContain('unsafe-eval');
      expect(headers['content-security-policy-report-only']).toContain(
        "require-trusted-types-for 'script'",
      );
      expect(await violations()).toEqual([]);
      expect(consoleErrors.filter((error) => /Content Security Policy/i.test(error))).toEqual([]);
    });
  }
});
