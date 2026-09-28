import type { SeoUrls } from '../../../../core/seo/urls/seo-urls';
import { trendsPageOf } from '../testing/trends-page-of';
import type { TrendsTexts } from './trends-texts';
import { trendsSeoPage } from './trends-seo-page';

const ORIGIN = 'https://league-of-data-base.com';
const URLS: SeoUrls = {
  origin: ORIGIN,
  canonical: `${ORIGIN}/fr/trends`,
  page: (path) => `${ORIGIN}/fr/${path}`,
  absolute: (url) => (url.startsWith('/') ? `${ORIGIN}${url}` : url),
};
const TEXTS: TrendsTexts = {
  seo: (key) => ({ 'trends.title': 'Tendances', 'trends.description': 'Les builds.' })[key] ?? key,
  main: (key) =>
    ({ 'header.navigation.home': 'Accueil', 'community.trends.title': 'Builds tendance' })[key] ??
    key,
};

describe('trendsSeoPage', () => {
  it('writes an indexable head at the address of the first page', () => {
    const page = trendsSeoPage(trendsPageOf().rows, TEXTS, 'fr');

    expect(page).toMatchObject({
      title: 'Tendances',
      description: 'Les builds.',
      locale: 'fr',
      path: 'trends',
    });
    expect(page.kind).toBeUndefined();
  });

  it('describes a trail from the home and the builds of the page in vote order', () => {
    const page = trendsSeoPage(trendsPageOf().rows, TEXTS, 'fr');
    const nodes = 'jsonLd' in page ? (page.jsonLd?.(URLS) ?? []) : [];

    expect(nodes.map((node) => node['@type'])).toEqual(['BreadcrumbList', 'ItemList']);
    expect(nodes[0]).toMatchObject({
      itemListElement: [
        { position: 1, name: 'Accueil', item: `${ORIGIN}/fr/` },
        { position: 2, name: 'Builds tendance', item: `${ORIGIN}/fr/trends` },
      ],
    });
    expect(nodes[1]).toMatchObject({
      numberOfItems: 2,
      itemListElement: [
        { position: 1, name: 'Mid burst', url: `${ORIGIN}/b/${'1'.padStart(24, 'a')}` },
        { position: 2, name: 'Old times', url: `${ORIGIN}/b/${'2'.padStart(24, 'a')}` },
      ],
    });
  });

  it('lists nothing when no build matches', () => {
    const page = trendsSeoPage([], TEXTS, 'fr');
    const nodes = 'jsonLd' in page ? (page.jsonLd?.(URLS) ?? []) : [];

    expect(nodes.map((node) => node['@type'])).toEqual(['BreadcrumbList']);
  });
});
