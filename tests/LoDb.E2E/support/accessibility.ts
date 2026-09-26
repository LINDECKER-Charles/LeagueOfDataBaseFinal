import AxeBuilder from '@axe-core/playwright';
import { expect, type Page } from '@playwright/test';

// The WCAG 2.1 A and AA rules, the level the site commits to.
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

/**
 * Runs axe on the page as rendered and fails on any violation, listed by rule id so the
 * report says what to fix.
 */
export async function expectNoAccessibilityViolations(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).withTags(WCAG_TAGS).analyze();
  expect(results.violations.map((violation) => violation.id)).toEqual([]);
}
