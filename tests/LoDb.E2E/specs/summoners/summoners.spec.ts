import type { APIRequestContext } from '@playwright/test';
import { type DetailNeighbours, expectServedDetail } from '../../support/catalog';
import { expect, test } from '../../support/test';

interface Meta {
  readonly latest: string | null;
}

interface Spell {
  readonly canonicalPath: string;
  readonly profile: { readonly name: string };
  readonly neighbours: DetailNeighbours;
}

// Flash is allowed in every mode.
const FLASH = 'SummonerFlash';

// The spell as /en/ shows it: the latest version, in en_US.
async function spellOf(request: APIRequestContext, id: string): Promise<Spell> {
  const meta = (await (await request.get('/api/meta')).json()) as Meta;
  expect(meta.latest, 'the stack must have ingested a version').not.toBeNull();
  const response = await request.get(`/api/catalog/${meta.latest}/en_US/summoners/${id}`);
  expect(response.status()).toBe(200);
  return (await response.json()) as Spell;
}

test.describe('summoner spell pages as crawlers read them', { tag: '@readonly' }, () => {
  test.use({ javaScriptEnabled: false });

  test('renders a spell in its seal, with its plaques and its modes', async ({ page, request }) => {
    const spell = await spellOf(request, FLASH);

    const response = await page.goto(`/en/${spell.canonicalPath}`);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText(spell.profile.name);
    await expectServedDetail(page, response, spell.neighbours);
    await expect(page.locator('.seal')).toBeVisible();
    expect(await page.locator('.hx-plate').count()).toBeGreaterThan(0);
    expect(await page.locator('[data-testid="modes"] li').count()).toBeGreaterThan(0);
  });
});
