import type { Page } from '@playwright/test';

/**
 * Records every violation the browser reports for the page's policies, from its very first
 * script: those it enforces and those it only reports (Trusted Types, Report-Only). Each one
 * fires this event before its report leaves for the API, so an empty list means no report.
 * Resolves to a reader of what was seen so far.
 */
export async function recordViolations(page: Page): Promise<() => Promise<string[]>> {
  await page.addInitScript(() => {
    const seen: string[] = [];
    Reflect.set(window, '__lodbViolations', seen);
    document.addEventListener('securitypolicyviolation', (event) => {
      seen.push(
        `${event.disposition} ${event.effectiveDirective} ${event.blockedURI} ${event.sourceFile}`,
      );
    });
  });
  return () => page.evaluate(() => (Reflect.get(window, '__lodbViolations') as string[]) ?? []);
}
