import { readHead } from '../../support/head';
import { expect, test } from '../../support/worker-account';
import { createBuild } from './builds';

// The build of the worker's account, deleted with the others once the test ends.
test('shares a private build with its link only, without a score', async ({
  page,
  member: owner,
  consoleErrors,
}) => {
  test.slow();
  const build = await createBuild(owner, {
    name: 'Hidden scroll',
    isPublic: false,
    language: 'en_US',
    gameMode: 'sr',
  });
  const path = `/b/${build.shareToken}`;

  await test.step('shows the build to anyone holding the link, unlisted', async () => {
    const response = await page.goto(path);
    expect(response?.status()).toBe(200);
    expect(response?.headers()['x-robots-tag']).toContain('noindex');
    expect((await readHead(page)).robots).toContain('noindex');
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.locator('.bshare-head h1')).toHaveText('Hidden scroll');
    await expect(page.locator('[data-mode]')).toHaveText("Summoner's Rift");
    await expect(page.getByRole('button', { name: 'Copy link' })).toBeVisible();
  });

  await test.step('shows no score, to a visitor or to its owner', async () => {
    await expect(page.locator('lodb-vote-score')).toHaveCount(0);
    await owner.goto(path);
    await expect(owner.locator('.bshare-head h1')).toHaveText('Hidden scroll');
    await expect(owner.locator('lodb-vote-score')).toHaveCount(0);
  });

  await test.step('lays the text of its player out in its own direction, in Arabic', async () => {
    await page.goto(`${path}?lang=ar_AE`);
    await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
    const description = page.locator('.bshare-desc');
    await expect(description).toHaveText('Hidden scroll, forged by the end-to-end suite.');
    // The full stop stays after the last word, on its right, never before the first one.
    const [first, stop] = await description.evaluate((element) => {
      const text = element.firstChild as Text;
      const leftOf = (index: number) => {
        const range = document.createRange();
        range.setStart(text, index);
        range.setEnd(text, index + 1);
        return range.getBoundingClientRect().left;
      };
      return [leftOf(text.data.indexOf('H')), leftOf(text.data.lastIndexOf('.'))];
    });
    expect(first).toBeLessThan(stop);
    // The name keeps the alignment of its column: it ends on the column's right edge.
    const [nameEnd, columnEnd] = await page.locator('.bshare-head h1').evaluate((heading) => {
      const range = document.createRange();
      range.selectNodeContents(heading);
      return [range.getBoundingClientRect().right, heading.getBoundingClientRect().right];
    });
    expect(Math.abs(nameEnd - columnEnd)).toBeLessThan(4);
  });

  await test.step('keeps it out of the trends', async () => {
    // A query of its own, which no proxy has cached yet.
    const query = `champion=${build.championId}&language=en_US&mode=sr&run=${build.shareToken}`;
    await page.goto(`/en/trends?${query}`);
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.locator(`a[href="${path}"]`)).toHaveCount(0);
  });

  expect(consoleErrors).toEqual([]);
});

test.describe('a link no build answers to', { tag: '@readonly' }, () => {
  for (const token of ['ffffffffffffffffffffffff', 'not-a-token']) {
    test(`answers /b/${token} with the 404`, async ({ request }) => {
      const response = await request.get(`/b/${token}`);

      expect(response.status()).toBe(404);
      expect(response.headers()['x-robots-tag']).toContain('noindex');
    });
  }
});
