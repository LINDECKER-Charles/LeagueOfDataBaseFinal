import { expect, type Locator, type Page } from '@playwright/test';

// A champion and an item every patch and Summoner's Rift carry: an import to an older patch
// keeps them, and the build stays savable.
export const CHAMPION = 'Annie';
export const ITEM = 'Long Sword';

/** The row of a build on the list of the account, found by its name. */
export function rowOf(page: Page, name: string): Locator {
  return page.locator('lodb-build-row').filter({ hasText: name });
}

/**
 * The patch of the build. Its label wraps the select, as the legacy editor's did: the label's
 * text holds every version offered, so the field is found by its accessible name, "Patch".
 */
export function patchOf(page: Page): Locator {
  return page.locator('lodb-editor-context').getByRole('combobox', { name: 'Patch', exact: true });
}

/** The purchase-order steps of the editor, in their order. */
export function stepsOf(page: Page): Locator {
  return page.locator('lodb-step-editor li.forge-step');
}

/** The rows of the rune board: the primary path's, then the secondary path's once chosen. */
export function runeRowsOf(page: Page): Locator {
  return page.locator('lodb-rune-board .rune-slot');
}

/** Chooses the champion through the picker's search; the chosen one shows on its toggle. */
export async function chooseChampion(page: Page, name: string): Promise<void> {
  await page.getByRole('button', { name: 'Choose a champion' }).click();
  await page.getByRole('searchbox', { name: 'Search a champion…' }).fill(name);
  await page.getByRole('listbox', { name: 'Champion' }).getByRole('option', { name }).click();
  await expect(page.locator('lodb-champion-picker .champ-toggle')).toContainText(name);
}

/**
 * Fills the rune page: the first path and its first rune of every row, then the first other
 * path and its first rune of its first two rows. Returns how many rows the primary path has.
 */
export async function pickRunes(page: Page): Promise<number> {
  const board = page.locator('lodb-rune-board');
  await board.getByRole('group', { name: 'Primary path' }).getByRole('button').first().click();
  const primaryRows = await runeRowsOf(page).count();
  for (let row = 0; row < primaryRows; row++) {
    await runeRowsOf(page).nth(row).locator('.rune-perk').first().click();
  }
  await board.getByRole('group', { name: 'Secondary path' }).getByRole('button').first().click();
  await runeRowsOf(page).nth(primaryRows).locator('.rune-perk').first().click();
  await runeRowsOf(page)
    .nth(primaryRows + 1)
    .locator('.rune-perk')
    .first()
    .click();
  await expect(board.locator('.rune-perk[aria-pressed="true"]')).toHaveCount(primaryRows + 2);
  return primaryRows;
}

/** Adds an item to a step through the armory, found by its search, then closes it. */
export async function addItem(page: Page, step: number, name: string): Promise<void> {
  await stepsOf(page).nth(step).getByRole('button', { name: 'Add item' }).click();
  const armory = page.getByRole('dialog', { name: 'Armory' });
  await armory.getByRole('searchbox', { name: 'Search an item…' }).fill(name);
  await armory.getByRole('button', { name }).first().click();
  await expect(armory.getByText('1 added')).toBeVisible();
  await armory.getByRole('button', { name: 'Done' }).click();
  await expect(armory).toHaveCount(0);
}
