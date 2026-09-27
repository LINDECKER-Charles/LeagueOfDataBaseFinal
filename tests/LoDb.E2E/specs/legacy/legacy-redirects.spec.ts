import type { APIRequestContext } from '@playwright/test';

import { expect, test } from '../../support/test';

// The 301 table of docs/reecriture/plan-migration.md, through nginx: every former URL lands
// in one hop on its new form, with and without ?lang= and a version. The targets are read
// from the API (latest version, canonical paths), never copied, so the suite follows Data
// Dragon.

const MOVED_PERMANENTLY = 301;
const OK = 200;
const NOT_FOUND = 404;
const REDIRECTION_FIRST = 300;
const REDIRECTION_LAST = 399;

interface Meta {
  readonly latest: string | null;
  readonly versions: readonly string[];
}

interface Detail {
  readonly canonicalPath: string;
}

interface Row {
  readonly old: string;
  readonly target: string;
}

interface Catalog {
  readonly latest: string;
  readonly previous: string;
  readonly paths: Readonly<Record<string, string>>;
}

// Old name → new detail request, below /api/catalog/{version}/en_US/.
const DETAILS: Readonly<Record<string, string>> = {
  'champion/Ahri': 'champions/Ahri',
  'object/1004': 'items/1004',
  'rune/Domination': 'runes/8100',
  'summoner/SummonerFlash': 'summoners/SummonerFlash',
};

async function readJson<T>(request: APIRequestContext, path: string): Promise<T> {
  const response = await request.get(path);
  expect(response.status(), path).toBe(OK);
  return (await response.json()) as T;
}

async function readCatalog(request: APIRequestContext): Promise<Catalog> {
  const meta = await readJson<Meta>(request, '/api/meta');
  const latest = meta.latest;
  if (latest === null) {
    throw new Error('No version is promoted yet: the stack has not ingested any.');
  }
  const previous = meta.versions[meta.versions.indexOf(latest) + 1];
  if (previous === undefined) {
    throw new Error(`No version is listed before ${latest}.`);
  }
  const paths: Record<string, string> = {};
  for (const [old, detail] of Object.entries(DETAILS)) {
    const body = await readJson<Detail>(request, `/api/catalog/${latest}/en_US/${detail}`);
    paths[old] = body.canonicalPath;
  }
  return { latest, previous, paths };
}

function path(catalog: Catalog, old: string): string {
  const canonical = catalog.paths[old];
  if (canonical === undefined) {
    throw new Error(`No canonical path read for ${old}.`);
  }
  return canonical;
}

function catalogRows(catalog: Catalog): Row[] {
  const { latest, previous } = catalog;
  return [
    { old: '/champions', target: '/en/champions' },
    { old: '/objects?lang=fr_FR', target: '/fr/items' },
    { old: '/runes', target: '/en/runes' },
    { old: '/summoners?lang=ko_KR', target: '/ko/summoners' },
    { old: `/${previous}/champions`, target: `/en/${previous}/champions` },
    { old: `/objects?version=${previous}&lang=fr_FR`, target: `/fr/${previous}/items` },
    { old: `/${latest}/runes`, target: '/en/runes' },
    { old: '/champion/Ahri', target: `/en/${path(catalog, 'champion/Ahri')}` },
    { old: '/object/1004?lang=fr_FR', target: `/fr/${path(catalog, 'object/1004')}` },
    { old: '/rune/Domination', target: `/en/${path(catalog, 'rune/Domination')}` },
    {
      old: '/summoner/SummonerFlash?lang=zh_CN',
      target: `/zh-hans/${path(catalog, 'summoner/SummonerFlash')}`,
    },
    {
      old: `/${latest}/champion/Ahri?lang=fr_FR`,
      target: `/fr/${path(catalog, 'champion/Ahri')}`,
    },
    {
      old: `/${latest}/object/1004`,
      target: `/en/${path(catalog, 'object/1004')}`,
    },
  ];
}

const PAGE_ROWS: readonly Row[] = [
  { old: '/home', target: '/en/' },
  { old: '/home?lang=fr_FR', target: '/fr/' },
  { old: '/trends', target: '/en/trends' },
  { old: '/about', target: '/en/about' },
  { old: '/about/data?lang=fr_FR', target: '/fr/about/data' },
  { old: '/faq', target: '/en/faq' },
  { old: '/changelog', target: '/en/changelog' },
  { old: '/developers', target: '/en/developers' },
  { old: '/donate', target: '/en/donate' },
  { old: '/legal/notice', target: '/en/legal/notice' },
  { old: '/legal/privacy?lang=fr_FR', target: '/fr/legal/privacy' },
  { old: '/legal/terms', target: '/en/legal/terms' },
  { old: '/legal/cookies', target: '/en/legal/cookies' },
];

// Client-rendered pages, or pages whose content depends on data the stack may not hold
// (a user): their target must not redirect, whatever it answers.
const UNCHECKED_TARGET_ROWS: readonly Row[] = [
  { old: '/u/Faker', target: '/en/u/Faker' },
  { old: '/login', target: '/en/account/login' },
  { old: '/register?lang=fr_FR', target: '/fr/account/register' },
  { old: '/profile', target: '/en/account/profile' },
  { old: '/profile/api', target: '/en/account/api' },
  { old: '/reset-password', target: '/en/account/forgot-password' },
  { old: '/builds', target: '/en/account/builds' },
  { old: '/builds/42/edit', target: '/en/account/builds/42/edit' },
];

async function expectOneHop(
  request: APIRequestContext,
  row: Row,
  targetStatus: number | null,
): Promise<void> {
  const response = await request.get(row.old, { maxRedirects: 0 });
  expect(response.status(), row.old).toBe(MOVED_PERMANENTLY);
  expect(response.headers()['location'], row.old).toBe(row.target);

  const landing = await request.get(row.target, { maxRedirects: 0 });
  const status = landing.status();
  expect(status >= REDIRECTION_FIRST && status <= REDIRECTION_LAST, row.target).toBe(false);
  if (targetStatus !== null) {
    expect(status, row.target).toBe(targetStatus);
  }
}

test.describe('legacy redirects', { tag: '@readonly' }, () => {
  test('catalog URLs land on the canonical page in one hop', async ({ request }) => {
    const catalog = await readCatalog(request);
    for (const row of catalogRows(catalog)) {
      await expectOneHop(request, row, OK);
    }
  });

  test('editorial pages land under the locale in one hop', async ({ request }) => {
    for (const row of PAGE_ROWS) {
      await expectOneHop(request, row, OK);
    }
  });

  test('account and profile pages land under the locale in one hop', async ({ request }) => {
    for (const row of UNCHECKED_TARGET_ROWS) {
      await expectOneHop(request, row, null);
    }
  });

  test('an unknown former URL is a real 404 page', async ({ request }) => {
    for (const old of ['/champion/NotAChampion', '/about/team', '/9.9.9/champions']) {
      const response = await request.get(old, { maxRedirects: 0 });
      expect(response.status(), old).toBe(NOT_FOUND);
      expect(response.headers()['content-type'], old).toContain('text/html');
    }
  });

  test('the contracts of the old site are not redirected', async ({ request }) => {
    for (const old of ['/b/000000000000000000000000', '/v1/champions', '/cdn/blobs/none.png']) {
      const response = await request.get(old, { maxRedirects: 0 });
      expect(response.status(), old).not.toBe(MOVED_PERMANENTLY);
    }
  });
});
