import { seoUrlsOf } from '../../urls/seo-urls-of';
import { aboutPage } from './about-page';
import { breadcrumbList } from './breadcrumb-list';
import { dataset } from './dataset';
import { faqPage } from './faq-page';
import { itemList } from './item-list';
import { profilePage } from './profile-page';
import { siteGraph } from './site-graph';

const ORIGIN = 'https://league-of-data-base.com';
const urls = seoUrlsOf({ origin: ORIGIN, locale: 'fr', version: null, path: 'about' });

describe('site JSON-LD builders', () => {
  it('links WebSite, Organization and WebPage in one graph', () => {
    expect(
      siteGraph({
        urls,
        pageName: 'À propos — League Of Data Base',
        siteDescription: 'Encyclopédie League of Legends',
        inLanguage: 'fr',
      }),
    ).toMatchSnapshot();
  });

  it('numbers a breadcrumb from the home', () => {
    expect(
      breadcrumbList([
        { name: 'Accueil', url: urls.page('') },
        { name: 'Champions', url: urls.page('champions') },
        { name: 'Ahri', url: urls.page('champions/Ahri') },
      ]),
    ).toMatchSnapshot();
  });

  it('lists twenty entries at most', () => {
    const entries = Array.from({ length: 25 }, (_, index) => ({
      name: `Champion ${index + 1}`,
      url: urls.page(`champions/C${index + 1}`),
    }));
    const list = itemList(entries);

    expect(list['numberOfItems']).toBe(20);
    expect(itemList(entries.slice(0, 2))).toMatchSnapshot();
  });

  it('describes the about page, the dataset and the FAQ', () => {
    expect(
      aboutPage({
        name: 'À propos',
        url: urls.canonical,
        description: ' Le projet ',
        inLanguage: 'fr',
      }),
    ).toMatchSnapshot();
    expect(
      dataset({
        name: 'League of Legends game data',
        url: urls.page('about/data'),
        description: 'Champions, objets, runes et sorts, patch par patch.',
        version: '16.19.1',
        languages: ['en', 'fr'],
        keywords: ['League of Legends', 'Data Dragon'],
        creatorId: `${ORIGIN}/#organization`,
      }),
    ).toMatchSnapshot();
    expect(
      faqPage(
        [
          { question: "D'où viennent les données ?", answer: 'De <b>Data Dragon</b>.' },
          { question: 'Sans réponse', answer: ' ' },
        ],
        urls.page('faq'),
      ),
    ).toMatchSnapshot();
  });

  it('describes a public profile without its absent fields', () => {
    expect(
      profilePage({ name: 'Faker', url: urls.page('u/Faker'), image: null, description: '' }),
    ).toMatchSnapshot();
  });
});
