import { HttpStatusCode } from '@angular/common/http';
import type { ResourceType } from '../api/generated/models/resource-type';
import { canonical } from './canonical';
import type { CanonicalFacts } from './canonical-facts';
import type { CataloguePage } from './catalogue-page';
import type { PageOutcome } from './outcome/page-outcome';

const LATEST = '16.19.1';
const OLDER = '15.14.1';
const VERSIONS = [LATEST, '16.18.1', OLDER];

interface Case {
  readonly rule: string;
  readonly page: Partial<CataloguePage> & { readonly resource: ResourceType };
  readonly canonicalPath?: string | null;
  readonly expected: PageOutcome | null;
}

const moved = (location: string): PageOutcome => ({
  kind: 'redirect',
  status: HttpStatusCode.MovedPermanently,
  location,
});
const found = (location: string): PageOutcome => ({
  kind: 'redirect',
  status: HttpStatusCode.Found,
  location,
});
const notFound: PageOutcome = { kind: 'not-found' };

const CASES: Case[] = [
  // The id decides; the slug is decorative.
  {
    rule: 'renders a canonical detail',
    page: { resource: 'items', entry: '1036-long-sword' },
    canonicalPath: 'items/1036-long-sword',
    expected: null,
  },
  {
    rule: 'adds a missing slug',
    page: { resource: 'items', entry: '1036' },
    canonicalPath: 'items/1036-long-sword',
    expected: moved('/en/items/1036-long-sword'),
  },
  {
    rule: 'fixes a wrong slug, keeping the query',
    page: { resource: 'runes', entry: '8000-domination', query: '?lang=en_GB' },
    canonicalPath: 'runes/8000-precision',
    expected: moved('/en/runes/8000-precision?lang=en_GB'),
  },
  {
    rule: 'fixes the slug below a pinned version',
    page: { resource: 'items', entry: '1036', pinned: OLDER, locale: 'fr' },
    canonicalPath: 'items/1036-long-sword',
    expected: moved(`/fr/${OLDER}/items/1036-long-sword`),
  },
  {
    rule: 'renders a champion by its id, which carries no slug',
    page: { resource: 'champions', entry: 'MonkeyKing' },
    canonicalPath: 'champions/MonkeyKing',
    expected: null,
  },
  {
    rule: 'renders a canonical detail of a pinned version',
    page: { resource: 'summoners', entry: 'SummonerFlash', pinned: OLDER },
    canonicalPath: 'summoners/SummonerFlash',
    expected: null,
  },
  // The latest version pinned in the path goes to the short URL.
  {
    rule: 'moves a list of the latest version to its short URL',
    page: { resource: 'champions', pinned: LATEST },
    expected: moved('/en/champions'),
  },
  {
    rule: 'moves a detail of the latest version before asking the API',
    page: { resource: 'champions', entry: 'Aatrox', pinned: LATEST, query: '?lang=en_GB' },
    expected: moved('/en/champions/Aatrox?lang=en_GB'),
  },
  {
    rule: 'moves a detail of the latest version and fixes its slug in the same hop',
    page: { resource: 'items', entry: '1036', pinned: LATEST },
    canonicalPath: 'items/1036-long-sword',
    expected: moved('/en/items/1036-long-sword'),
  },
  // A missing entity.
  {
    rule: 'sends a missing entity of a pinned version to that version list',
    page: { resource: 'champions', entry: 'Nope', pinned: OLDER, query: '?lang=fr_FR' },
    canonicalPath: null,
    expected: found(`/en/${OLDER}/champions?lang=fr_FR`),
  },
  {
    rule: 'answers 404 for a missing entity of the latest version',
    page: { resource: 'champions', entry: 'Nope' },
    canonicalPath: null,
    expected: notFound,
  },
  // Unknown versions.
  {
    rule: 'answers 404 for a list of a version Data Dragon does not list',
    page: { resource: 'champions', pinned: '99.99.1' },
    expected: notFound,
  },
  {
    rule: 'answers 404 for a detail of a version Data Dragon does not list',
    page: { resource: 'items', entry: '1036-long-sword', pinned: '99.99.1' },
    canonicalPath: 'items/1036-long-sword',
    expected: notFound,
  },
  // The version of the query moves into the path.
  {
    rule: 'moves a known ?version= into the path, keeping the other parameters',
    page: { resource: 'items', query: '?tag=Boots%2CArmor&version=15.14.1&page=2' },
    expected: moved(`/en/${OLDER}/items?tag=Boots%2CArmor&page=2`),
  },
  {
    rule: 'drops a ?version= naming the latest',
    page: { resource: 'champions', entry: 'Aatrox', query: `?version=${LATEST}` },
    expected: moved('/en/champions/Aatrox'),
  },
  {
    rule: 'ignores an unknown ?version=',
    page: { resource: 'champions', query: '?version=99.99.1' },
    expected: null,
  },
  {
    rule: 'lets the path win over the query',
    page: { resource: 'champions', pinned: OLDER, query: '?version=16.18.1' },
    expected: null,
  },
  // Pages the rules leave alone.
  {
    rule: 'renders a list of the latest version',
    page: { resource: 'runes', query: '?page=2' },
    expected: null,
  },
  {
    rule: 'renders a detail whose entity has not been asked yet',
    page: { resource: 'items', entry: '1036' },
    expected: null,
  },
];

function pageOf(page: Case['page']): CataloguePage {
  return { locale: 'en', pinned: null, entry: null, query: '', ...page };
}

describe('canonical (ADR 0005)', () => {
  it.each(CASES)('$rule', ({ page, canonicalPath, expected }) => {
    const facts: CanonicalFacts = { latest: LATEST, versions: VERSIONS, canonicalPath };

    expect(canonical(pageOf(page), facts)).toEqual(expected);
  });

  it('never redirects to the short URL before the first ingestion', () => {
    const facts: CanonicalFacts = { latest: null, versions: VERSIONS };

    expect(canonical(pageOf({ resource: 'champions', pinned: OLDER }), facts)).toBeNull();
  });
});
