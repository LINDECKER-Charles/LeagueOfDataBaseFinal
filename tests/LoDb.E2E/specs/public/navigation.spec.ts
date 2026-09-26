import type { Page } from '@playwright/test';
import { expect, test } from '../../support/test';

// Heritage H2: navigation by the header and the bottom bar through the router, section
// anchors, and the state of the filters in the URL, on the four lists.
const CODEX = ['champions', 'items', 'runes', 'summoners'];
const BOTTOM_BAR = ['', 'champions', 'items', 'runes', 'summoners', 'trends'];
const DESKTOP = { width: 1280, height: 800 };
const PHONE = { width: 390, height: 844 };
const MARK = 'lodbSameDocument';
const CHAMPION = '/en/champions/Annie';
const SECTIONS = ['abilities', 'skins', 'lore', 'tips', 'stats'];
// A search that keeps a few cards of each list.
const LISTS = [
  { resource: 'champions', search: 'Annie' },
  { resource: 'items', search: 'Infinity Edge' },
  { resource: 'runes', search: 'Electrocute' },
  { resource: 'summoners', search: 'Flash' },
];
const CARD = 'lodb-catalogue-list .grid__cell';
const SEARCH = 'lodb-filter-console input[type=search]';
const ACTIVE_CHIP = 'lodb-active-filters .active__chip';

function pathOf(page: Page): string {
  return new URL(page.url()).pathname;
}

// Marks the document: still marked later, the pages changed through the router.
async function markDocument(page: Page): Promise<void> {
  await page.waitForLoadState('networkidle');
  await page.evaluate((mark) => Reflect.set(window, mark, true), MARK);
}

async function isSameDocument(page: Page): Promise<boolean> {
  return page.evaluate((mark) => Reflect.get(window, mark) === true, MARK);
}

test.describe('navigation by the header and the bottom bar', () => {
  test('opens each list of the codex from the header menu, through the router', async ({
    page,
    consoleErrors,
  }) => {
    await page.setViewportSize(DESKTOP);
    await page.goto('/en/');
    await markDocument(page);
    const menu = page.locator('header details.switcher--nav');

    for (const resource of CODEX) {
      if (!(await menu.evaluate((details: HTMLDetailsElement) => details.open))) {
        await menu.locator('summary').click();
      }
      const link = menu.locator(`a[href="/en/${resource}"]`);
      await link.click();

      await expect.poll(() => pathOf(page)).toBe(`/en/${resource}`);
      await expect(link).toHaveAttribute('aria-current', 'page');
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    }
    expect(await isSameDocument(page)).toBe(true);
    expect(consoleErrors).toEqual([]);
  });

  test('leads through the bottom bar on a phone, which marks the current page', async ({
    page,
  }) => {
    await page.setViewportSize(PHONE);
    await page.goto('/en/about');
    await markDocument(page);
    const bar = page.locator('nav.bottom-nav');
    const links = bar.getByRole('link');

    await expect(page.locator('header nav')).toBeHidden();
    await expect(links).toHaveCount(BOTTOM_BAR.length);
    for (const [index, path] of BOTTOM_BAR.entries()) {
      await links.nth(index).click();

      await expect.poll(() => pathOf(page)).toBe(`/en/${path}`);
      await expect(links.nth(index)).toHaveAttribute('aria-current', 'page');
      await expect(bar.locator('a[aria-current="page"]')).toHaveCount(1);
    }
    expect(await isSameDocument(page)).toBe(true);
  });
});

test.describe('section anchors', () => {
  test('jump to a section, mark its chip and leave a deep link', async ({ page }) => {
    await page.goto(CHAMPION);
    await page.waitForLoadState('networkidle');
    const nav = page.locator('lodb-section-nav nav');

    for (const id of ['lore', 'abilities', 'tips']) {
      const chip = nav.locator(`a[href="${CHAMPION}#${id}"]`);
      await chip.click();

      await expect.poll(() => new URL(page.url()).hash).toBe(`#${id}`);
      await expect(page.locator(`#${id}`)).toBeInViewport();
      await expect(chip).toHaveAttribute('aria-current', 'true');
    }
    expect(pathOf(page)).toBe(CHAMPION);
  });

  test('open a deep link at its section', async ({ page }) => {
    await page.goto(`${CHAMPION}#lore`);
    await page.waitForLoadState('networkidle');

    await expect(page.locator('#lore')).toBeInViewport();
  });

  test.describe('without JavaScript', () => {
    test.use({ javaScriptEnabled: false });

    test('are plain links to the sections of the page', async ({ page }) => {
      await page.goto(CHAMPION);

      const hrefs = await page
        .locator('lodb-section-nav a')
        .evaluateAll((links) => links.map((link) => link.getAttribute('href')));
      expect(hrefs).toEqual(SECTIONS.map((id) => `${CHAMPION}#${id}`));
      for (const id of SECTIONS) {
        await expect(page.locator(`#${id}`)).toHaveCount(1);
      }
    });
  });
});

test.describe('filters in the URL', () => {
  for (const { resource, search } of LISTS) {
    test(`keeps the filters of /en/${resource} in the URL, which opens them again`, async ({
      page,
      context,
    }) => {
      await page.goto(`/en/${resource}`);
      await page.waitForLoadState('networkidle');
      const total = await page.locator(CARD).count();

      await page.locator(SEARCH).fill(search);
      await expect.poll(() => new URL(page.url()).searchParams.get('q')).toBe(search);
      const before = [...new URL(page.url()).searchParams.keys()].length;
      await page.locator('lodb-filter-console .chip:not([disabled])').first().click();
      await expect.poll(() => [...new URL(page.url()).searchParams.keys()].length).toBe(before + 1);
      const shown = await page.locator(CARD).count();
      const chips = await page.locator(ACTIVE_CHIP).count();
      expect(shown).toBeLessThan(total);

      const copy = await context.newPage();
      await copy.goto(page.url());
      await expect(copy.locator(SEARCH)).toHaveValue(search);
      await expect(copy.locator(CARD)).toHaveCount(shown);
      await expect(copy.locator(ACTIVE_CHIP)).toHaveCount(chips);
    });
  }
});
