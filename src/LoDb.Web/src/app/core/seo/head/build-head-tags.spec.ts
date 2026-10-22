import { breadcrumbList } from '../json-ld/site/breadcrumb-list';
import type { SeoPage } from '../seo-page';
import { buildHeadTags } from './build-head-tags';
import type { HeadTags } from './head-tags';

const ORIGIN = 'https://league-of-data-base.com';
const PINNED = '11.7.1';
const OPENER = '11.5.1';
const TEXTS = {
  siteTitle: 'League of Data Base',
  siteDescription: "L'encyclopédie de LoL.",
  versionedSuffix: `(version ${PINNED})`,
};
const MOLTEN_EDGE = 'items/3371-molten-edge';

// Molten Edge at a pinned version; each case hands the canonical the API answers for it.
const PAGE: SeoPage = {
  title: 'Lame en fusion',
  locale: 'fr',
  path: MOLTEN_EDGE,
  version: PINNED,
  jsonLd: (urls) => [breadcrumbList([{ name: 'Lame en fusion', url: urls.canonical }])],
};

function headOf(page: SeoPage): HeadTags {
  return buildHeadTags({ page, texts: TEXTS, origin: ORIGIN, locale: 'fr' });
}

function hrefs(head: HeadTags, rel: string): string[] {
  return head.links.filter((link) => link.rel === rel).map((link) => link.href);
}

function meta(head: HeadTags, key: string): string | undefined {
  return head.metas.find((tag) => tag.key === key)?.content;
}

// Indexed under its own URL: canonical on itself, its 21 locales at the same version and the
// default (en), the site graph and its own nodes.
function expectIndexedHere(head: HeadTags, version: string | null, path: string): void {
  const address = `${version === null ? '' : `${version}/`}${path}`;
  const alternates = hrefs(head, 'alternate');
  expect(hrefs(head, 'canonical')).toEqual([`${ORIGIN}/fr/${address}`]);
  expect(meta(head, 'robots')).toBe('index, follow');
  expect(alternates).toHaveLength(22);
  expect(alternates.at(-1)).toBe(`${ORIGIN}/en/${address}`);
  expect(head.jsonLd).toHaveLength(2);
}

// Indexed elsewhere: its canonical alone, indexable so that crawlers follow its links.
function expectIndexedAt(head: HeadTags, url: string): void {
  expect(hrefs(head, 'canonical')).toEqual([url]);
  expect(meta(head, 'og:url')).toBe(url);
  expect(meta(head, 'robots')).toBe('index, follow');
  expect(hrefs(head, 'alternate')).toEqual([]);
  expect(head.jsonLd).toEqual([]);
}

// One case per row of the canonical rule (docs/produit/seo-une-page-par-etat.md, § 3.3).
describe('buildHeadTags, the canonical of each page', () => {
  it('keeps a page of the latest version its own canonical, whatever it hands over', () => {
    const head = headOf({
      ...PAGE,
      version: null,
      canonical: { version: OPENER, path: 'items/1001-boots' },
    });

    expectIndexedHere(head, null, MOLTEN_EDGE);
  });

  it('indexes the opener of a closed state under its own URL, alternates included', () => {
    const head = headOf({ ...PAGE, canonical: { version: PINNED, path: MOLTEN_EDGE } });

    expectIndexedHere(head, PINNED, MOLTEN_EDGE);
    expect(JSON.stringify(head.jsonLd[1])).toContain(`/fr/${PINNED}/${MOLTEN_EDGE}`);
  });

  it('points another version of a closed state to its opener, same locale', () => {
    const head = headOf({ ...PAGE, canonical: { version: OPENER, path: MOLTEN_EDGE } });

    expectIndexedAt(head, `${ORIGIN}/fr/${OPENER}/${MOLTEN_EDGE}`);
  });

  // The open state, a version past the index's coverage end and a version outside the index
  // whose entity the latest version holds: the API names the short URL in all three.
  it('points a page of the open state to the short URL, at the latest path', () => {
    const head = headOf({ ...PAGE, canonical: { version: null, path: 'items/3371-blade' } });

    expectIndexedAt(head, `${ORIGIN}/fr/items/3371-blade`);
  });

  it('indexes a page outside the index the latest version lacks under its own URL', () => {
    const head = headOf({ ...PAGE, canonical: { version: PINNED, path: MOLTEN_EDGE } });

    expectIndexedHere(head, PINNED, MOLTEN_EDGE);
  });

  it('points a list of a pinned version to the bare list of the latest one', () => {
    const head = headOf({ title: 'Objets', locale: 'fr', path: 'items', version: PINNED });

    expectIndexedAt(head, `${ORIGIN}/fr/items`);
  });

  it('indexes the page of the latest patch that changed something', () => {
    const head = headOf({ ...PAGE, path: 'changes', version: null });

    expectIndexedHere(head, null, 'changes');
  });

  it('indexes the page of a past patch under its own URL, given explicitly', () => {
    const head = headOf({
      ...PAGE,
      path: 'changes',
      canonical: { version: PINNED, path: 'changes' },
    });

    expectIndexedHere(head, PINNED, 'changes');
  });

  it('keeps the page of a patch that changed nothing out of the index, links followed', () => {
    const head = headOf({
      ...PAGE,
      kind: 'noindex',
      path: 'changes',
      canonical: { version: PINNED, path: 'changes' },
    });

    expect(meta(head, 'robots')).toBe('noindex, follow');
    expect(hrefs(head, 'canonical')).toEqual([`${ORIGIN}/fr/${PINNED}/changes`]);
    expect(hrefs(head, 'alternate')).toEqual([]);
    expect(head.jsonLd).toEqual([]);
  });
});
