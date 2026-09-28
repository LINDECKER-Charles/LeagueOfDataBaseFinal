import type { SeoUrls } from '../../../core/seo/urls/seo-urls';
import { publicProfileOf } from '../testing/public-profile-of';
import { profileSeoPage } from './profile-seo-page';

const ORIGIN = 'https://league-of-data-base.com';
const URLS: SeoUrls = {
  origin: ORIGIN,
  canonical: `${ORIGIN}/fr/u/Faker`,
  page: (path) => `${ORIGIN}/fr/${path}`,
  absolute: (url) => (url.startsWith('/') ? `${ORIGIN}${url}` : url),
};
const TEXTS: Record<string, string> = {
  'profile.public.description': 'La carte de {username}.',
  'header.navigation.home': 'Accueil',
};

// A catalogue that fills its placeholders, enough to see what the page hands over.
function translate(key: string, params: Record<string, string> = {}): string {
  const text = TEXTS[key] ?? key;
  return Object.entries(params).reduce(
    (out, [name, value]) => out.replace(`{${name}}`, value),
    text,
  );
}

describe('profileSeoPage', () => {
  it('titles the card with the display name, its skin splash as the preview image', () => {
    const page = profileSeoPage(publicProfileOf(), translate, 'fr');

    expect(page).toMatchObject({
      title: 'Faker#KR1',
      description: 'La carte de Faker#KR1.',
      locale: 'fr',
      image: '/cdn/ahri-7.jpg',
      ogType: 'profile',
      path: 'u/Faker',
    });
    expect(page.kind).toBeUndefined();
  });

  it('describes a trail from the home, the profile page and the builds it lists', () => {
    const page = profileSeoPage(publicProfileOf(), translate, 'fr');
    const nodes = 'jsonLd' in page ? (page.jsonLd?.(URLS) ?? []) : [];

    expect(nodes.map((node) => node['@type'])).toEqual([
      'BreadcrumbList',
      'ProfilePage',
      'ItemList',
    ]);
    expect(nodes[0]).toMatchObject({
      itemListElement: [
        { name: 'Accueil', item: `${ORIGIN}/fr/` },
        { name: 'Faker#KR1', item: `${ORIGIN}/fr/u/Faker` },
      ],
    });
    expect(nodes[1]).toMatchObject({
      mainEntity: {
        name: 'Faker#KR1',
        url: `${ORIGIN}/fr/u/Faker`,
        image: `${ORIGIN}/cdn/ahri-7.jpg`,
        description: 'La carte de Faker#KR1.',
      },
    });
    expect(nodes[2]).toMatchObject({
      numberOfItems: 2,
      itemListElement: [
        { position: 1, name: 'Mid burst', url: `${ORIGIN}/b/k3y-1` },
        { position: 2, name: 'Old times', url: `${ORIGIN}/b/k3y-2` },
      ],
    });
  });

  it('lists no builds and shows the site image for a card without them or a skin', () => {
    const card = publicProfileOf({ riotTagline: null, builds: [] });
    const page = profileSeoPage(
      { ...card, showcase: { ...card.showcase, skin: null } },
      translate,
      'fr',
    );
    const nodes = 'jsonLd' in page ? (page.jsonLd?.(URLS) ?? []) : [];

    expect(page).toMatchObject({ title: 'Faker', image: null });
    expect(nodes.map((node) => node['@type'])).toEqual(['BreadcrumbList', 'ProfilePage']);
    expect(nodes[1]).not.toHaveProperty('mainEntity.image');
  });
});
