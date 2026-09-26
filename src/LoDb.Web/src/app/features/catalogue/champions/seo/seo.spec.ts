import { ApplicationRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ChampionCard } from '../../../../core/api/generated/models/champion-card';
import type { ChampionDetails } from '../../../../core/api/generated/models/champion-details';
import type { ChampionList } from '../../../../core/api/generated/models/champion-list';
import type { PageContext } from '../../../../core/context/page-context';
import { Seo } from '../../../../core/seo/seo';
import type { SeoPage } from '../../../../core/seo/seo-page';
import type { SeoUrls } from '../../../../core/seo/urls/seo-urls';
import { applyChampionsHead } from './apply-champions-head';
import { championSeo } from './champion-seo';
import { championsListSeo } from './champions-list-seo';
import type { HeadSource } from './head-source';
import type { Translate } from './translate';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const PINNED: PageContext = { ...CONTEXT, version: '15.14.1', pinned: true };
const URLS: SeoUrls = {
  origin: 'https://lodb.example',
  canonical: 'https://lodb.example/en/champions/Annie',
  page: (path) => `https://lodb.example/en/${path}`,
  absolute: (url) => url,
};

// Echoes the key and its parameters, so a test sees what was asked for.
const translate: Translate = (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key);

const ANNIE: ChampionCard = {
  canonicalPath: 'champions/Annie',
  id: 'Annie',
  key: '1',
  name: 'Annie',
  title: 'the Dark Child',
  image: { status: 'absent' },
  partype: 'Mana',
  resource: 'mana',
  stats: [{ stat: 'health', base: 560, perLevel: 96 }],
  tags: ['Mage'],
};

const DETAILS: ChampionDetails = {
  canonicalPath: 'champions/Annie',
  version: '16.19.1',
  language: 'en_US',
  profile: ANNIE,
  art: { splash: 'splash.jpg', centered: 'centered.jpg', loading: 'loading.jpg' },
  blurb: 'Dangerous, yet disarmingly precocious.',
  abilities: [],
  skins: [],
  allyTips: [],
  enemyTips: [],
};

const LIST: ChampionList = {
  entries: [ANNIE, { ...ANNIE, canonicalPath: 'champions/Brand', id: 'Brand', name: 'Brand' }],
  total: 172,
  version: '16.19.1',
  language: 'en_US',
  facets: { resources: [], tags: [] },
};

function nodesOf(page: SeoPage): unknown[] {
  return 'jsonLd' in page && page.jsonLd ? [...page.jsonLd(URLS)] : [];
}

describe('championSeo', () => {
  it('names the champion and the version in the texts of the seo scope', () => {
    expect(championSeo({ context: CONTEXT, details: DETAILS }, translate)).toMatchObject({
      title: 'seo.champion.detail.title {"name":"Annie"}',
      description: 'seo.champion.detail.description {"name":"Annie","version":"16.19.1"}',
      locale: 'en',
      image: '/preview/champions.png',
      path: 'champions/Annie',
      version: null,
    });
  });

  it('is self-canonical on a pinned version', () => {
    const page = championSeo({ context: PINNED, details: DETAILS }, translate);
    expect(page).toMatchObject({ version: '15.14.1' });
  });

  it('describes the path home › champions › champion, then the champion as a character', () => {
    const [breadcrumb, character] = nodesOf(
      championSeo({ context: CONTEXT, details: DETAILS }, translate),
    );
    expect(breadcrumb).toMatchObject({
      '@type': 'BreadcrumbList',
      itemListElement: [
        { position: 1, name: 'header.navigation.home', item: 'https://lodb.example/en/' },
        { position: 2, name: 'header.navigation.champion', item: URLS.page('champions') },
        { position: 3, name: 'Annie', item: URLS.canonical },
      ],
    });
    expect(JSON.stringify(character)).toContain('"name":"Annie"');
  });
});

describe('championsListSeo', () => {
  it('counts the champions of the version and lists the ones rendered', () => {
    const page = championsListSeo(CONTEXT, LIST, translate);
    expect(page).toMatchObject({
      title: 'seo.champion.list.title',
      description: 'seo.champion.list.description {"count":172,"version":"16.19.1"}',
      path: 'champions',
      image: '/preview/champions.png',
    });
    expect(nodesOf(page)).toEqual([
      expect.objectContaining({ '@type': 'ItemList', numberOfItems: 2 }),
    ]);
  });

  it('keeps its title, and the site description, when the list did not come', () => {
    const page = championsListSeo(PINNED, null, translate);
    expect(page).toMatchObject({ title: 'seo.champion.list.title', version: '15.14.1' });
    expect(page.description).toBeUndefined();
    expect(nodesOf(page)).toEqual([expect.objectContaining({ numberOfItems: 0 })]);
  });
});

describe('applyChampionsHead', () => {
  const CATALOGUES: Record<string, object> = {
    fr: { header: { navigation: { champion: 'Champions' } } },
    'seo/fr': { champion: { detail: { title: '{{ name }}, champion' } } },
  };

  function render(source: () => HeadSource | null) {
    const apply = vi.fn<(page: SeoPage) => Promise<void>>().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [
        { provide: Seo, useValue: { apply } },
        provideTransloco({
          config: {
            availableLangs: ['en', 'fr'],
            defaultLang: 'en',
            missingHandler: { logMissingKey: false },
            prodMode: true,
          },
          loader: class {
            getTranslation = (path: string) => of(CATALOGUES[path] ?? {});
          },
        }),
      ],
    });
    TestBed.runInInjectionContext(() => applyChampionsHead(source));
    return apply;
  }

  it('applies the head in the page locale once its catalogues are loaded', async () => {
    const apply = render(() => ({
      locale: 'fr',
      build: (t) => ({ title: t('seo.champion.detail.title', { name: 'Annie' }), path: '' }),
    }));

    await TestBed.inject(ApplicationRef).whenStable();

    expect(apply).toHaveBeenCalledWith({ title: 'Annie, champion', path: '' });
  });

  it('applies it again when the source changes, and nothing without a source', async () => {
    const name = signal<string | null>(null);
    const apply = render(() => {
      const current = name();
      return current === null
        ? null
        : { locale: 'fr', build: () => ({ title: current, path: '' }) };
    });
    await TestBed.inject(ApplicationRef).whenStable();
    expect(apply).not.toHaveBeenCalled();

    name.set('Brand');
    await TestBed.inject(ApplicationRef).whenStable();

    expect(apply).toHaveBeenCalledExactlyOnceWith({ title: 'Brand', path: '' });
  });
});
