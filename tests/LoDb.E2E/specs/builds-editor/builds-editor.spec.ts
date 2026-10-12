import { metaOf, olderVersion } from '../../support/catalog';
import { recordViolations } from '../../support/policy-violations';
import { expect, test } from '../../support/worker-account';
import {
  addItem,
  CHAMPION,
  chooseChampion,
  dragTo,
  ITEM,
  patchOf,
  pickRunes,
  rowOf,
  stepsOf,
} from './forge';

// The forge as a hand uses it: the CDK's drag and drop needs a real layout and pointer, the
// dialogs run under the site's policy, and the import reads a stored older patch from the real
// API. The forge's rules and states are Vitest's and the API's.
const LIST = '/en/account/builds';
const SHARED = /\/b\/[\w-]+$/;
const EDITING = /\/en\/account\/builds\/\d+\/edit$/;
const NAME = 'E2E lethality carry';
const RENAMED = 'E2E lethality carry, revised';

// The worker's verified account: the API limits how many are created in a row. The forge
// refused to an unverified account is left to verifiedEmailGuard's unit test and the API's.
test('forges, edits, imports and deletes a build', async ({ member: page, request }) => {
  const meta = await metaOf(request);
  // The pages of the account and the editor, with its dialogs, run under the site's policy.
  const violations = await recordViolations(page);

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

  await test.step('chooses the champion, the runes and a first step', async () => {
    await chooseChampion(page, CHAMPION);
    await pickRunes(page);
    await stepsOf(page).first().getByLabel('Step label').fill('Core');
    await addItem(page, 0, ITEM);
  });

  await test.step('forges the build and opens its shared page', async () => {
    await page.getByLabel('Build name').fill(NAME);
    await page.getByRole('button', { name: 'Forge the build' }).click();
    await expect(page).toHaveURL(SHARED);
    await expect(page.getByText('Build forged!')).toBeVisible();
    // Read before leaving the document: each page load starts a list of its own.
    expect(await violations(), 'no violation in the forge').toEqual([]);
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
    // As on the legacy card, an item dropped anywhere on a step lands in it, an empty step too.
    await test.step('moves an item onto the note of the empty step', async () => {
      const [first, second] = [stepsOf(page).nth(0), stepsOf(page).nth(1)];
      // Both cards in view: the empty one at the bottom, the item right above it.
      await second.evaluate((step) => step.scrollIntoView({ block: 'end' }));
      const note = second.getByLabel('Note (optional)');
      await dragTo(page, first.locator('.forge-slot').first(), note);
      await expect(second.locator('.forge-slot')).toHaveCount(1);
      await expect(first.locator('.forge-slot')).toHaveCount(0);
    });
    await stepsOf(page).nth(1).getByLabel('Step label').fill('Final');
    await addItem(page, 0, ITEM);
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

  expect(await violations(), 'no violation on the list').toEqual([]);
});
