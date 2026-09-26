import type { APIRequestContext } from '@playwright/test';
import { expect, test } from '../../support/test';

interface Meta {
  readonly latest: string | null;
}

interface Mode {
  readonly code: string;
  readonly label?: string | null;
}

interface Spell {
  readonly canonicalPath: string;
  readonly profile: {
    readonly name: string;
    readonly modes: readonly Mode[];
    readonly counterpart?: { readonly canonicalPath?: string | null } | null;
  };
}

const LIST = '/en/summoners';
const CARD = 'lodb-catalogue-list .grid__cell';
// Flash is allowed in every mode, and has a LoL Classic twin.
const FLASH = 'SummonerFlash';
// A mode without a label of its own is named by its edition (the LoL Classic client).
const CLASSIC_LABEL = 'LoL Classic';

// The spell as /en/ shows it: the latest version, in en_US.
async function spellOf(request: APIRequestContext, id: string): Promise<Spell> {
  const meta = (await (await request.get('/api/meta')).json()) as Meta;
  expect(meta.latest, 'the stack must have ingested a version').not.toBeNull();
  const response = await request.get(`/api/catalog/${meta.latest}/en_US/summoners/${id}`);
  expect(response.status()).toBe(200);
  return (await response.json()) as Spell;
}

test.describe('summoner spell pages as crawlers read them', () => {
  test.use({ javaScriptEnabled: false });

  test('renders the list, each card with its cooldown, linking its spell', async ({ page }) => {
    await page.goto(LIST);

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    expect(await page.locator(CARD).count()).toBeGreaterThan(0);
    await expect(page.locator(`${CARD} a`).first()).toHaveAttribute('href', /^\/en\/summoners\//);
    await expect(page.locator(`${CARD} .stat-cell`).first()).toBeVisible();
  });

  test('renders a spell in its seal, with its plaques and its named modes', async ({
    page,
    request,
  }) => {
    const spell = await spellOf(request, FLASH);

    await page.goto(`/en/${spell.canonicalPath}`);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText(spell.profile.name);
    await expect(page.locator('.seal')).toBeVisible();
    expect(await page.locator('.hx-plate').count()).toBeGreaterThan(0);
    const modes = await page.locator('[data-testid="modes"] li').allTextContents();
    expect(modes.length).toBeGreaterThan(0);
    // ARAM or URF are names in capitals: only a code the API names otherwise is a raw key.
    const labels = spell.profile.modes.map((mode) => mode.label ?? CLASSIC_LABEL);
    const rawKeys = spell.profile.modes
      .filter((mode) => mode.label !== mode.code)
      .map((mode) => mode.code);
    for (const mode of modes.map((text) => text.trim())) {
      expect(labels).toContain(mode);
      expect(rawKeys).not.toContain(mode);
    }
  });
});

test.describe('summoner spell pages', () => {
  test('link a spell to its LoL Classic twin and back', async ({
    page,
    request,
    consoleErrors,
  }) => {
    const spell = await spellOf(request, FLASH);
    const twin = spell.profile.counterpart?.canonicalPath;
    test.skip(!twin, 'the stack carries no LoL Classic edition');

    await page.goto(`/en/${spell.canonicalPath}`);
    await page.locator('lodb-edition-counterpart a[data-edition="classic"]').click();

    await expect(page).toHaveURL(new RegExp(`/en/${twin}$`));
    await expect(page.locator('header lodb-edition-badge')).toContainText('LoL Classic');
    await expect(page.locator('lodb-edition-counterpart a[data-edition="modern"]')).toHaveAttribute(
      'href',
      `/en/${spell.canonicalPath}`,
    );
    expect(consoleErrors).toEqual([]);
  });
});
