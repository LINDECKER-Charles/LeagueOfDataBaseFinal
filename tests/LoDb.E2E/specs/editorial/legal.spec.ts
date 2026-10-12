import type { Locator } from '@playwright/test';
import { expect, test } from '../../support/test';

// A jump to a section needs a real layout: where it lands under the sticky header, where the
// focus goes, and the scroll Back restores, none of which jsdom lays out. The table of
// contents itself is Vitest's.

// The scroll margin of a prose section (6.5rem), a pixel short for rounding.
const SCROLL_MARGIN = 103;

async function topOf(section: Locator): Promise<number> {
  return section.evaluate((element) => element.getBoundingClientRect().top);
}

test.describe('legal pages', { tag: '@readonly' }, () => {
  test('scroll to a section from the table of contents', async ({ page }) => {
    await page.goto('/en/legal/cookies');
    await page.waitForLoadState('networkidle');
    const contents = page.getByRole('navigation', { name: 'Contents' });
    const link = contents.getByRole('link').nth(2);
    const target = ((await link.getAttribute('href')) ?? '').split('#')[1] ?? '';

    await link.click();

    await expect(page).toHaveURL(new RegExp(`/en/legal/cookies#${target}$`));
    const section = page.locator(`section#${target}`);
    await expect(section).toBeInViewport();
    // Below its scroll margin (6.5rem), clear of the sticky header, as a native jump lands.
    expect(await topOf(section)).toBeGreaterThanOrEqual(SCROLL_MARGIN);
    // As after a native jump: the next Tab starts in the section, and Back returns to the top.
    await expect(section).toBeFocused();
    await page.goBack();
    await expect(page).toHaveURL(/\/en\/legal\/cookies$/);
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  });
});
