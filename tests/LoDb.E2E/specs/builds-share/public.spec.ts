import type { Page } from '@playwright/test';
import { readHead } from '../../support/head';
import { expect, test } from '../../support/worker-account';
import { createBuild, voteSaved } from './builds';

// The build is written in French: its page speaks it, unless `?lang=` names another language,
// the one the visitor picked in the switcher (as the legacy session did).
const LANGUAGE = 'fr_FR';

function voteScore(page: Page) {
  return page.locator('.bshare-head .vote-score');
}

// The owner, the worker's account, and a visitor, around a public build, then a private one.
test('shares a public build with its score, and a private one by its link alone', async ({
  page,
  member: owner,
  workerAccount: account,
  consoleErrors,
}) => {
  test.slow();
  const build = await createBuild(owner, {
    name: 'Shared scroll',
    isPublic: true,
    language: LANGUAGE,
    gameMode: 'aram',
  });
  const path = `/b/${build.shareToken}`;

  await test.step('speaks the language ?lang= names, the build’s own without one', async () => {
    await page.goto(`${path}?lang=en_US`);
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.locator('.bshare-head h1')).toHaveText('Shared scroll');
    await expect(page.locator('[data-mode]')).toHaveText('ARAM');
    await expect(page.locator('[data-version]')).toContainText(build.gameVersion);
    await expect(page.locator('.bshare-head')).toContainText(`By ${account.username}`);
    await expect(page.locator('.bsteps-node .bshare-item')).toHaveCount(2);
  });

  await test.step('sends a visitor to sign in before voting', async () => {
    await page.goto(path);
    await expect(voteScore(page)).toHaveText('0');
    const login = page.getByRole('link', { name: 'Connectez-vous pour voter' }).first();
    await expect(login).toHaveAttribute(
      'href',
      `/fr/account/login?returnUrl=${encodeURIComponent(path)}`,
    );
  });

  await test.step('lets a signed-in reader vote, keeps the vote, and withdraws it', async () => {
    await owner.goto(path);
    const up = owner.getByRole('button', { name: 'Voter pour ce build' });
    const voted = voteSaved(owner, build);
    await up.click();
    await expect(voteScore(owner)).toHaveText('+1');
    await expect(up).toHaveAttribute('aria-pressed', 'true');
    expect((await voted).ok(), 'the API keeps the vote').toBe(true);
    // The server renders the score for nobody; the reader's own vote comes back after it.
    await owner.reload();
    await expect(up).toHaveAttribute('aria-pressed', 'true');
    await expect(voteScore(owner)).toHaveText('+1');
    const withdrawn = voteSaved(owner, build);
    await up.click();
    await expect(voteScore(owner)).toHaveText('0');
    await expect(up).toHaveAttribute('aria-pressed', 'false');
    expect((await withdrawn).ok(), 'the API withdraws the vote').toBe(true);
  });

  const hidden = await createBuild(owner, {
    name: 'Hidden scroll',
    isPublic: false,
    language: 'en_US',
    gameMode: 'sr',
  });
  const hiddenPath = `/b/${hidden.shareToken}`;

  await test.step('shows a private build to anyone holding the link, unlisted', async () => {
    const response = await page.goto(hiddenPath);
    expect(response?.status()).toBe(200);
    expect(response?.headers()['x-robots-tag']).toContain('noindex');
    expect((await readHead(page)).robots).toContain('noindex');
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await expect(page.locator('.bshare-head h1')).toHaveText('Hidden scroll');
    await expect(page.locator('[data-mode]')).toHaveText("Summoner's Rift");
    await expect(page.getByRole('button', { name: 'Copy link' })).toBeVisible();
  });

  await test.step('shows no score on it, to a visitor or to its owner', async () => {
    await expect(page.locator('lodb-vote-score')).toHaveCount(0);
    await owner.goto(hiddenPath);
    await expect(owner.locator('.bshare-head h1')).toHaveText('Hidden scroll');
    await expect(owner.locator('lodb-vote-score')).toHaveCount(0);
  });

  expect(consoleErrors).toEqual([]);
});
