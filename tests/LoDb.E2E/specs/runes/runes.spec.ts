import { expect, test } from '../../support/test';

// A rune of the list leads to its card on its path's page: the router lands on an anchor of
// another page and scrolls it into view, which needs a real layout and viewport. The path's
// page and its anchors are Vitest's.
const LIST = '/en/runes';
const CARD = 'lodb-catalogue-list lodb-rune-card';

test.describe('rune pages', { tag: '@readonly' }, () => {
  test('lead from a rune to its card on its path page', async ({ page, consoleErrors }) => {
    await page.goto(LIST);
    await page.waitForLoadState('networkidle');
    const link = page.locator(`${CARD} a`).first();
    const anchor = (await link.getAttribute('href'))?.split('#')[1] ?? '';

    await link.click();

    await expect(page).toHaveURL(new RegExp(`/en/runes/[^#]+#${anchor}$`));
    const card = page.locator(`#${anchor}`);
    await expect(card).toBeInViewport();
    await page.locator('lodb-pager .pager__hub').click();
    await expect(page).toHaveURL(/\/en\/runes$/);
    expect(consoleErrors).toEqual([]);
  });
});
