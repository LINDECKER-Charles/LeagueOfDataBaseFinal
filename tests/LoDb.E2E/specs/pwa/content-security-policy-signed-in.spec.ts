import { addItem, CHAMPION, chooseChampion, ITEM } from '../builds-editor/forge';
import { recordViolations } from '../../support/policy-violations';
import { expect, test } from '../../support/worker-account';

// What a signed-in member goes through, rendered in the browser: the account's pages and the
// build editor, with its dialogs.
test.describe('the content security policy of the account', () => {
  test('lets the profile and the build editor run without a violation', async ({ member }) => {
    const violations = await recordViolations(member);

    await member.goto('/en/account/profile');
    await member.waitForLoadState('networkidle');
    await member.goto('/en/account/builds/new');
    await chooseChampion(member, CHAMPION);
    await addItem(member, 0, ITEM);

    expect(await violations()).toEqual([]);
  });
});
