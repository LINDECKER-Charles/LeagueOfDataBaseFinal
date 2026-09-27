import type { APIRequestContext } from '@playwright/test';
import { idsOf, metaOf, olderVersion, type CatalogMeta } from '../../support/catalog';
import { expect, test } from '../../support/test';

// The outcomes of ADR 0005, one row per rule: a decorative slug and the latest version in the
// path answer a 301 to the canonical URL, an entity absent from a pinned version a 302 to
// that version's list, anything else unknown a real 404. Each answer is transient (it may
// stop being true at the next patch) and lands in one hop. The legacy table is
// specs/legacy/legacy-redirects.spec.ts's; its rows that meet these rules are repeated here.
const TRANSIENT = 'public, max-age=0, s-maxage=60';
const NOINDEX = 'noindex';
// Enough patches back for the catalogue to have grown since.
const DISTANT_PATCHES = 40;

interface Row {
  readonly from: string;
  readonly status: 301 | 302 | 404;
  /** Path and query of the Location, for a redirect. */
  readonly to?: string;
}

function adrRows(latest: string): Row[] {
  return [
    { from: '/en/items/3031', status: 301, to: '/en/items/3031-infinity-edge' },
    { from: '/en/items/3031-wrong-slug', status: 301, to: '/en/items/3031-infinity-edge' },
    { from: '/en/runes/8100', status: 301, to: '/en/runes/8100-domination' },
    {
      from: '/ko/items/3031?lang=ko_KR',
      status: 301,
      to: '/ko/items/3031-infinity-edge?lang=ko_KR',
    },
    { from: `/en/${latest}/champions/Annie`, status: 301, to: '/en/champions/Annie' },
    { from: `/fr/${latest}/items`, status: 301, to: '/fr/items' },
    { from: '/en/champions/NoSuchChampion', status: 404 },
    { from: '/en/items/999999', status: 404 },
    { from: '/en/1.0.0/champions', status: 404 },
    { from: '/en/nowhere', status: 404 },
    { from: '/xx/champions', status: 404 },
  ];
}

function legacyRows(latest: string): Row[] {
  return [
    { from: `/${latest}/champion/Annie`, status: 301, to: '/en/champions/Annie' },
    { from: '/object/771004', status: 301, to: '/en/items/771004-faerie-charm' },
    { from: '/rune/Domination?lang=ja_JP', status: 301, to: '/ja/runes/8100-domination' },
  ];
}

// A champion of the latest version the catalogue did not have `DISTANT_PATCHES` ago.
async function absentRow(request: APIRequestContext, meta: CatalogMeta): Promise<Row | null> {
  const distant = olderVersion(meta, DISTANT_PATCHES);
  const then = new Set(await idsOf(request, distant, 'champions'));
  const newcomer = (await idsOf(request, meta.latest, 'champions')).find((id) => !then.has(id));
  if (newcomer === undefined) return null;
  const list = `/en/${distant}/champions`;
  return { from: `${list}/${newcomer}`, status: 302, to: list };
}

function locationOf(location: string | undefined): string {
  const url = new URL(location ?? '', 'http://location.invalid');
  return `${url.pathname}${url.search}`;
}

async function expectOutcome(request: APIRequestContext, row: Row): Promise<void> {
  const response = await request.get(row.from, { maxRedirects: 0 });
  const headers = response.headers();

  expect(response.status(), row.from).toBe(row.status);
  if (row.to === undefined) {
    expect(headers['x-robots-tag'], row.from).toBe(NOINDEX);
    return;
  }
  expect(locationOf(headers['location']), row.from).toBe(row.to);
  const landing = await request.get(row.to, { maxRedirects: 0 });
  expect(landing.status(), `${row.from} → ${row.to} lands in one hop`).toBe(200);
}

test.describe('outcomes of the URL grammar', { tag: '@readonly' }, () => {
  test('redirect a decorative slug and the latest version, answer 404 for the unknown', async ({
    request,
  }) => {
    const { latest } = await metaOf(request);

    for (const row of adrRows(latest)) {
      await expectOutcome(request, row);
      const response = await request.get(row.from, { maxRedirects: 0 });
      expect(response.headers()['cache-control'], row.from).toBe(TRANSIENT);
    }
  });

  test('send an entity absent from a pinned version to that version list', async ({ request }) => {
    const row = await absentRow(request, await metaOf(request));
    test.skip(row === null, 'no champion joined the catalogue in the patches the stack knows');

    await expectOutcome(request, row as Row);
  });

  test('move a former URL meeting these rules in one hop', async ({ request }) => {
    const { latest } = await metaOf(request);

    for (const row of legacyRows(latest)) {
      await expectOutcome(request, row);
    }
  });
});

// `/` by Accept-Language: the first language the site speaks wins, region aside; Chinese
// goes by script, else by region; nothing spoken falls back to English, the x-default.
const PREFERENCES: readonly (readonly [string, string])[] = [
  ['fr-FR,fr;q=0.9,en;q=0.8', '/fr/'],
  ['pt-BR,pt;q=0.9', '/pt/'],
  ['en-GB', '/en/'],
  ['ja-JP', '/ja/'],
  ['ar-EG,ar;q=0.9', '/ar/'],
  ['zh-TW', '/zh-hant/'],
  ['zh-HK', '/zh-hant/'],
  ['zh-Hant-SG', '/zh-hant/'],
  ['zh-CN', '/zh-hans/'],
  ['zh', '/zh-hans/'],
  ['sv-SE,de;q=0.8', '/de/'],
  ['de;q=0.5,it;q=0.9', '/it/'],
  ['nl-NL,sv;q=0.8', '/en/'],
];

test.describe('entry at /', { tag: '@readonly' }, () => {
  for (const [header, target] of PREFERENCES) {
    test(`sends Accept-Language "${header}" to ${target}`, async ({ request }) => {
      const response = await request.get('/', {
        headers: { 'Accept-Language': header },
        maxRedirects: 0,
      });

      expect(response.status()).toBe(302);
      expect(locationOf(response.headers()['location'])).toBe(target);
      expect(response.headers()['vary']).toContain('Accept-Language');
    });
  }
});
