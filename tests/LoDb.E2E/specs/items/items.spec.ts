import { expect, test } from '../../support/test';

// A card of the list the server rendered leads to its item, whose pager leads back: the
// hydration of two pages and the router between them only run in a browser on the stack. The
// item's page as crawlers read it is champions.spec.ts's sample; its content is Vitest's.
const LIST = '/en/items';
const CARD = 'lodb-catalogue-list .grid__cell';

test.describe('item pages', { tag: '@readonly' }, () => {
  test('lead from a card to its item, then back to the list', async ({ page, consoleErrors }) => {
    await page.goto(LIST);
    await page.waitForLoadState('networkidle');

    await page.locator(`${CARD} a`).first().click();

    await expect(page).toHaveURL(/\/en\/items\/.+/);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.locator('lodb-pager a[rel="next"], lodb-pager a[rel="prev"]').first().waitFor();
    await page.locator('lodb-pager .pager__hub').click();
    await expect(page).toHaveURL(/\/en\/items$/);
    expect(consoleErrors).toEqual([]);
  });
});
