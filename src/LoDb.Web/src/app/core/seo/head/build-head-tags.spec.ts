import { breadcrumbList } from '../json-ld/site/breadcrumb-list';
import type { SeoPage } from '../seo-page';
import { buildHeadTags } from './build-head-tags';
import type { HeadTags } from './head-tags';

const ORIGIN = 'https://league-of-data-base.com';
const PINNED = '8.7.1';
const TEXTS = {
  siteTitle: 'League of Data Base',
  siteDescription: "L'encyclopédie de LoL.",
  versionedSuffix: `(version ${PINNED})`,
};

// Molten Edge, held by 8.7.1 and dropped since: the API places it there.
const MOLTEN_EDGE: SeoPage = {
  title: 'Lame en fusion',
  locale: 'fr',
  path: 'items/3371-molten-edge',
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

// A page of a pinned version (ADR 0005, amendment of 2026-10-14). The head of a latest page
// is seo.spec.ts'.
describe('buildHeadTags on a pinned version', () => {
  it('points an entity the latest version holds to its short URL, alone', () => {
    const head = headOf({
      ...MOLTEN_EDGE,
      path: 'items/1001-boots-of-speed',
      canonical: { version: null, path: 'items/1001-boots' },
    });

    expect(hrefs(head, 'canonical')).toEqual([`${ORIGIN}/fr/items/1001-boots`]);
    expect(meta(head, 'og:url')).toBe(`${ORIGIN}/fr/items/1001-boots`);
    expect(meta(head, 'robots')).toBe('index, follow');
    expect(hrefs(head, 'alternate')).toEqual([]);
    expect(head.jsonLd).toEqual([]);
  });

  it('points a page the API places nowhere in particular to the same page at the latest', () => {
    const head = headOf({ title: 'Objets', locale: 'fr', path: 'items', version: PINNED });

    expect(hrefs(head, 'canonical')).toEqual([`${ORIGIN}/fr/items`]);
    expect(meta(head, 'robots')).toBe('index, follow');
  });

  it('indexes a dropped entity in the last version holding it, alternates included', () => {
    const head = headOf({ ...MOLTEN_EDGE, canonical: { version: PINNED, path: MOLTEN_EDGE.path } });
    const alternates = hrefs(head, 'alternate');

    expect(hrefs(head, 'canonical')).toEqual([`${ORIGIN}/fr/${PINNED}/items/3371-molten-edge`]);
    expect(meta(head, 'robots')).toBe('index, follow');
    expect(alternates).toHaveLength(22);
    expect(alternates.filter((href) => !href.includes(`/${PINNED}/`))).toEqual([]);
    expect(head.jsonLd).toHaveLength(2);
    expect(JSON.stringify(head.jsonLd[1])).toContain(`/fr/${PINNED}/items/3371-molten-edge`);
  });

  it('keeps an earlier page of a dropped entity out of the index, its links followed', () => {
    const head = headOf({ ...MOLTEN_EDGE, version: '7.22.1', canonical: null });

    expect(meta(head, 'robots')).toBe('noindex, follow');
    expect(hrefs(head, 'canonical')).toEqual([]);
    expect(meta(head, 'og:url')).toBeUndefined();
    expect(hrefs(head, 'alternate')).toEqual([]);
    expect(head.jsonLd).toEqual([]);
  });

  it('keeps a page of the latest version its own canonical, whatever it hands over', () => {
    const head = headOf({
      ...MOLTEN_EDGE,
      path: 'items/1001-boots',
      version: null,
      canonical: null,
    });

    expect(hrefs(head, 'canonical')).toEqual([`${ORIGIN}/fr/items/1001-boots`]);
    expect(meta(head, 'robots')).toBe('index, follow');
    expect(hrefs(head, 'alternate')).toHaveLength(22);
  });
});
