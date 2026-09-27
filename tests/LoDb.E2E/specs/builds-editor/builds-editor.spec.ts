import { metaOf, olderVersion } from '../../support/catalog';
import { expect, test } from '../../support/worker-account';
import {
  addItem,
  CHAMPION,
  chooseChampion,
  ITEM,
  patchOf,
  pickRunes,
  rowOf,
  runeRowsOf,
  stepsOf,
} from './forge';

const LIST = '/en/account/builds';
const SHARED = /\/b\/[\w-]+$/;
const EDITING = /\/en\/account\/builds\/\d+\/edit$/;
const NAME = 'E2E lethality carry';
const RENAMED = 'E2E lethality carry, revised';
const PHONE = { width: 390, height: 844 };
const DESKTOP = { width: 1280, height: 720 };

// No account is created here: what a visitor meets on the builds of an account.
test('sends a visitor from the builds to the sign-in, which brings them back', async ({ page }) => {
  await page.goto(LIST);

  await expect(page).toHaveURL(/\/en\/account\/login\?/);
  expect(new URL(page.url()).searchParams.get('returnUrl')).toBe(LIST);
});

// The worker's verified account: the API limits how many are created in a row. The forge
// refused to an unverified account is left to verifiedEmailGuard's unit test and the API's.
test('forges, edits, imports and deletes a build', async ({ member: page, request }) => {
  const meta = await metaOf(request);

  await test.step('lists no build yet, and leads to the forge on the latest patch', async () => {
    await page.goto(LIST);
    await expect(page.getByRole('heading', { name: 'My builds' })).toBeVisible();
    // The site's empty state, as the legacy list drew it: a way back and home, then the forge.
    const empty = page.locator('lodb-my-builds .hextech-frame');
    await expect(empty).toContainText("You haven't forged any build yet.");
    await expect(empty.getByRole('button', { name: 'Back' })).toBeVisible();
    await expect(empty.getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/en/');
    await page.getByRole('link', { name: 'Forge my first build' }).click();
    await expect(page).toHaveURL(`${LIST}/new`);
    await expect(page.getByRole('heading', { name: 'Forge a build' })).toBeVisible();
    await expect(patchOf(page)).toHaveValue(meta.latest);
  });

  await test.step('drops the oldest secondary rune for a third one', async () => {
    await chooseChampion(page, CHAMPION);
    const primaryRows = await pickRunes(page);
    const oldest = runeRowsOf(page).nth(primaryRows);
    await runeRowsOf(page)
      .nth(primaryRows + 2)
      .locator('.rune-perk')
      .first()
      .click();
    await expect(oldest.locator('.rune-perk[aria-pressed="true"]')).toHaveCount(0);
    await expect(page.locator('lodb-rune-board .rune-perk[aria-pressed="true"]')).toHaveCount(
      primaryRows + 2,
    );
  });

  await test.step('opens the armory as a bottom sheet on a phone', async () => {
    await page.setViewportSize(PHONE);
    await stepsOf(page).first().getByRole('button', { name: 'Add item' }).click();
    const armory = page.getByRole('dialog', { name: 'Armory' });
    await expect(armory).toBeVisible();
    await expect(page.locator('.hx-sheet')).toHaveCount(1);
    // Exact: item names such as "Crystalline Bracer" contain "all" too.
    await expect(armory.getByRole('button', { name: 'All', exact: true })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    await armory.getByRole('button', { name: 'Done' }).click();
    await expect(armory).toHaveCount(0);
    await page.setViewportSize(DESKTOP);
  });

  // As the legacy form: the browser stops a short name, the API names the rest in toasts.
  await test.step('stops a short name, then shows the refusal of the server', async () => {
    await stepsOf(page).first().getByLabel('Step label').fill('Core');
    await addItem(page, 0, ITEM);
    const name = page.getByLabel('Build name');
    await name.fill('ab');
    await page.getByRole('button', { name: 'Forge the build' }).click();
    await expect(name).toBeFocused();
    expect(await name.evaluate((field: HTMLInputElement) => field.validity.tooShort)).toBe(true);

    // Three characters for the browser, two once the API has trimmed them.
    await name.fill('ab ');
    await page.getByRole('button', { name: 'Forge the build' }).click();
    await expect(page.getByText('Build name must be 3–80 characters.')).toBeVisible();
    await expect(page).toHaveURL(`${LIST}/new`);
  });

  await test.step('forges the build and opens its shared page', async () => {
    await page.getByLabel('Build name').fill(NAME);
    await page.getByRole('button', { name: 'Forge the build' }).click();
    await expect(page).toHaveURL(SHARED);
    await expect(page.getByText('Build forged!')).toBeVisible();
    await page.goto(LIST);
    await expect(rowOf(page, NAME)).toContainText(CHAMPION);
    await expect(rowOf(page, NAME)).toContainText('Private');
  });

  await test.step('edits it: renamed, public, a second step moved first', async () => {
    await rowOf(page, NAME).getByRole('link', { name: 'Edit' }).click();
    await expect(page).toHaveURL(EDITING);
    await expect(page.getByRole('heading', { name: 'Edit build' })).toBeVisible();
    await expect(page.getByLabel('Build name')).toHaveValue(NAME);
    await expect(stepsOf(page).first().locator('.forge-slot')).toHaveCount(1);

    await page.getByLabel('Build name').fill(RENAMED);
    await page.getByLabel('Public build — listed on your public profile').check();
    await page.getByRole('button', { name: 'Add a step' }).click();
    await stepsOf(page).nth(1).getByLabel('Step label').fill('Final');
    await addItem(page, 1, ITEM);
    // The step's own "Move up" comes before those of its items.
    await stepsOf(page).nth(1).getByRole('button', { name: 'Move up' }).first().click();
    await expect(stepsOf(page).first().getByLabel('Step label')).toHaveValue('Final');

    await page.getByRole('button', { name: 'Save changes' }).click();
    await expect(page).toHaveURL(SHARED);
    await expect(page.getByText('Build updated.')).toBeVisible();
    await page.goto(LIST);
    await expect(rowOf(page, RENAMED)).toContainText('Public');
  });

  // As in the legacy editor, an import opens a create-mode editor: the source is kept, and
  // forging the ported draft adds a second build.
  await test.step('imports it to the previous patch, reviewed then forged anew', async () => {
    const older = olderVersion(meta);
    const row = rowOf(page, RENAMED);
    await row.getByLabel('Import to patch').selectOption(older);
    await row.getByRole('button', { name: 'Import' }).click();
    await expect(page).toHaveURL(new RegExp(`/import\\?to=${older.replaceAll('.', '\\.')}$`));
    await expect(page.getByText(`Build imported to patch ${older}`)).toBeVisible();
    await expect(patchOf(page)).toHaveValue(older);
    await expect(page.getByLabel('Build name')).toHaveValue(RENAMED);

    await page.getByRole('button', { name: 'Forge the build' }).click();
    await expect(page).toHaveURL(SHARED);
    await page.goto(LIST);
    await expect(rowOf(page, RENAMED)).toHaveCount(2);
  });

  await test.step('deletes both once confirmed, the list empty again', async () => {
    const confirm = page.getByRole('dialog', { name: 'Delete the build' });
    await rowOf(page, RENAMED).first().getByRole('button', { name: 'Delete' }).click();
    // The frame's close button is named "Cancel" too: the dialog's own comes last.
    await confirm.getByRole('button', { name: 'Cancel' }).last().click();
    await expect(confirm).toHaveCount(0);
    await expect(rowOf(page, RENAMED)).toHaveCount(2);

    for (const left of [1, 0]) {
      await rowOf(page, RENAMED).first().getByRole('button', { name: 'Delete' }).click();
      await confirm.getByRole('button', { name: 'Delete', exact: true }).click();
      await expect(page.getByText('Build deleted.').first()).toBeVisible();
      await expect(rowOf(page, RENAMED)).toHaveCount(left);
    }
    await expect(page.getByText("You haven't forged any build yet.")).toBeVisible();
  });
});
