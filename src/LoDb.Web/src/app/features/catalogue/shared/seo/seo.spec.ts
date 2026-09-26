import type { PageContext } from '../../../../core/context/page-context';
import type { SeoUrls } from '../../../../core/seo/urls/seo-urls';
import { catalogueListSeo } from './catalogue-list-seo';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const URLS: SeoUrls = {
  origin: 'https://lodb.example',
  canonical: 'https://lodb.example/en/champions',
  page: (path) => `https://lodb.example/en/${path}`,
  absolute: (url) => url,
};

describe('catalogueListSeo', () => {
  const entries = [
    { name: 'Aatrox', canonicalPath: 'champions/Aatrox' },
    { name: 'Ahri', canonicalPath: 'champions/Ahri' },
  ];
  const base = { title: 'Champions', description: 'All champions', path: 'champions', entries };

  it('addresses the bare list, with the version only when pinned', () => {
    expect(catalogueListSeo({ ...base, context: CONTEXT })).toMatchObject({
      title: 'Champions',
      path: 'champions',
      locale: 'en',
      version: null,
    });
    const pinned = { ...CONTEXT, version: '15.14.1', pinned: true };
    expect(catalogueListSeo({ ...base, context: pinned })).toMatchObject({ version: '15.14.1' });
  });

  it('lists the rendered entries, linked to their pages', () => {
    const page = catalogueListSeo({ ...base, context: CONTEXT });
    const nodes = 'jsonLd' in page && page.jsonLd ? page.jsonLd(URLS) : [];
    expect(nodes).toEqual([
      expect.objectContaining({
        '@type': 'ItemList',
        numberOfItems: 2,
        itemListElement: [
          { '@type': 'ListItem', position: 1, name: 'Aatrox', url: URLS.page('champions/Aatrox') },
          { '@type': 'ListItem', position: 2, name: 'Ahri', url: URLS.page('champions/Ahri') },
        ],
      }),
    ]);
  });
});
