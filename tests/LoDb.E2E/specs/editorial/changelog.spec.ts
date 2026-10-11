import { expect, test } from '../../support/test';

test.describe('changelog', { tag: '@readonly' }, () => {
  test('lists the releases newest first, the newest one open', async ({ page, consoleErrors }) => {
    await page.goto('/en/changelog');
    await page.waitForLoadState('networkidle');
    const releases = page.locator('.timeline > li');

    expect(await releases.count()).toBeGreaterThan(1);
    await expect(releases.first().locator('details')).toHaveAttribute('open', '');
    await expect(releases.nth(1).locator('details')).not.toHaveAttribute('open');
    await expect(releases.first()).toHaveAttribute('id', /^v\d+-\d+-\d+$/);
    expect(consoleErrors).toEqual([]);
  });

  // Until the changelog page has a component spec of its own (lower-layer gap G5).
  test('marks the French notes of a release read under another locale', async ({ page }) => {
    await page.goto('/de/changelog');

    await expect(page.locator('.timeline > li').first().locator('.release__body')).toHaveAttribute(
      'lang',
      'fr',
    );
  });
});
