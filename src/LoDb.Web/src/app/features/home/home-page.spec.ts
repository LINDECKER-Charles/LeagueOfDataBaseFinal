import { inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type ActivatedRouteSnapshot, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation, TranslocoService } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ResourceType } from '../../core/api/generated/models/resource-type';
import type { Locale } from '../../core/i18n/locales';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Seo } from '../../core/seo/seo';
import type { HomeData } from './data/home-data';
import type { HomeSection } from './data/home-section';
import { HomePage } from './home-page';

const ORIGIN = 'https://league-of-data-base.com';
const VERSION = '16.19.1';
const SEO_TITLE = 'League Of Data Base — League of Legends encyclopedia';
const LOADING_ART = 'https://ddragon.leagueoflegends.com/cdn/img/champion/loading/Aatrox_0.jpg';
const CATALOGUES: Record<string, Translation> = {
  en: {
    homepage: {
      title: 'League of Data Base',
      hero: { browse: 'Updated for patch <strong>{{ version }}</strong>.' },
      champions: { title: 'Champions', see_all: 'See all champions' },
      items: { title: 'Items', see_all: 'See all items' },
      runes: { title: 'Runes', see_all: 'See all runes' },
      summoners: { title: 'Summoner Spells', see_all: 'See all spells' },
    },
  },
  'seo/en': {
    home: { title: SEO_TITLE, description: 'Everything of patch {{ version }}.' },
  },
  'seo/fr': {
    home: {
      title: 'League Of Data Base — encyclopédie',
      description: 'Tout le patch {{ version }}.',
    },
  },
  'home/en': { patch: 'Patch', empty: 'Nothing to show for this version.' },
};

function sectionOf(locale: string, resource: ResourceType, filled: boolean): HomeSection {
  const card = {
    link: { path: `/${locale}/${resource}/first`, query: {} },
    name: `First of ${resource}`,
    caption: 'caption',
    image: null,
    art: resource === 'champions' ? LOADING_ART : null,
  };
  return {
    resource,
    list: { path: `/${locale}/${resource}`, query: {} },
    total: filled ? 4 : null,
    cards: filled ? [card] : [],
  };
}

// What resolveHome gives: every section filled but the summoner spells, out of reach.
function homeOf(route: ActivatedRouteSnapshot): HomeData {
  const locale = route.paramMap.get('locale') as Locale;
  const section = (resource: ResourceType) => sectionOf(locale, resource, resource !== 'summoners');
  return {
    context: { locale, version: VERSION, pinned: false, language: 'en_US' },
    sections: {
      champions: section('champions'),
      items: section('items'),
      runes: section('runes'),
      summoners: section('summoners'),
    },
  };
}

// One route for every locale, as app.routes.ts mounts the home: the page is then reused.
async function visit(url: string) {
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: ORIGIN },
      provideRouter([
        {
          path: ':locale',
          runGuardsAndResolvers: 'paramsOrQueryParamsChange',
          // The root catalogue, which activateLocale loads in the application.
          resolve: { home: homeOf, catalogue: () => inject(TranslocoService).load('en') },
          component: HomePage,
        },
      ]),
      provideTransloco({
        config: {
          availableLangs: ['en', 'fr'],
          defaultLang: 'en',
          fallbackLang: 'en',
          missingHandler: { logMissingKey: false, useFallbackTranslation: true },
          prodMode: true,
        },
        loader: class {
          getTranslation = (path: string) => of(CATALOGUES[path] ?? {});
        },
      }),
    ],
  });
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  return { harness, apply, host: harness.routeNativeElement as HTMLElement };
}

function headElements(selector: string): Element[] {
  return [...document.head.querySelectorAll(`[data-lodb-seo]${selector}`)];
}

function hrefOf(selector: string): string | null | undefined {
  return headElements(selector)[0]?.getAttribute('href');
}

function textsOf(host: HTMLElement, selector: string): string[] {
  return [...host.querySelectorAll(selector)].map((element) => element.textContent?.trim() ?? '');
}

describe('HomePage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    headElements('').forEach((element) => element.remove());
  });

  it('titles the document with the SEO title alone and describes its version', async () => {
    document.documentElement.lang = 'fr';

    const { apply } = await visit('/fr');

    expect(apply).toHaveBeenLastCalledWith({
      title: 'League Of Data Base — encyclopédie',
      description: `Tout le patch ${VERSION}.`,
      path: '',
      titleFormat: 'raw',
      locale: 'fr',
    });
    expect(document.title).toBe('League Of Data Base — encyclopédie');
  });

  it('is indexable, canonical at the root of its locale, with 21 alternates and x-default', async () => {
    document.documentElement.lang = 'fr';

    await visit('/fr');
    const hreflangs = headElements('link[rel="alternate"]').map((link) =>
      link.getAttribute('hreflang'),
    );

    expect(headElements('link[rel="canonical"]')).toHaveLength(1);
    expect(hrefOf('link[rel="canonical"]')).toBe(`${ORIGIN}/fr/`);
    expect(headElements('meta[name="robots"]')[0]?.getAttribute('content')).toBe('index, follow');
    expect(new Set(hreflangs).size).toBe(22);
    expect(hreflangs.at(-1)).toBe('x-default');
    expect(hrefOf('link[rel="alternate"][hreflang="x-default"]')).toBe(`${ORIGIN}/`);
  });

  it('keeps its canonical free of the query that picks its context', async () => {
    document.documentElement.lang = 'en';

    await visit('/en?version=15.14.1&lang=en_GB');

    expect(hrefOf('link[rel="canonical"]')).toBe(`${ORIGIN}/en/`);
  });

  it('rewrites its head when the router reuses it for another locale', async () => {
    document.documentElement.lang = 'fr';
    const { harness, apply } = await visit('/fr');

    document.documentElement.lang = 'en';
    await harness.navigateByUrl('/en');
    await harness.fixture.whenStable();

    expect(apply).toHaveBeenLastCalledWith(expect.objectContaining({ locale: 'en' }));
    expect(document.title).toBe(SEO_TITLE);
    expect(hrefOf('link[rel="canonical"]')).toBe(`${ORIGIN}/en/`);
  });

  it('names its version, its language and the size of each list in the hero', async () => {
    document.documentElement.lang = 'en';

    const { host } = await visit('/en');

    const hero = host.querySelector('section') as HTMLElement;
    expect(hero.querySelector('h1')?.textContent?.trim()).toBe('League of Data Base');
    expect(hero.textContent).toContain(VERSION);
    expect(hero.textContent).toContain('en_US');
    expect(hero.querySelector('strong')?.textContent).toBe(VERSION);
  });

  it('opens four portals into the catalogue, counted when their list answered', async () => {
    document.documentElement.lang = 'en';

    const { host } = await visit('/en');

    const portals = [...host.querySelectorAll<HTMLAnchorElement>('section ul a')];
    expect(portals.map((portal) => portal.getAttribute('href'))).toEqual([
      '/en/champions',
      '/en/items',
      '/en/runes',
      '/en/summoners',
    ]);
    expect(portals[0]?.textContent).toContain('4');
  });

  it('previews each resource in the legacy order, an empty section saying so', async () => {
    document.documentElement.lang = 'en';

    const { host } = await visit('/en');

    expect(textsOf(host, 'lodb-preview-section h2')).toEqual([
      'Champions',
      'Items',
      'Summoner Spells',
      'Runes',
    ]);
    const champions = host.querySelector('lodb-preview-section') as HTMLElement;
    expect(champions.querySelector('li a')?.getAttribute('href')).toBe('/en/champions/first');
    const spells = host.querySelectorAll('lodb-preview-section')[2] as HTMLElement;
    expect(spells.querySelector('p')?.textContent?.trim()).toBe(
      'Nothing to show for this version.',
    );
  });

  it('draws each champion in its loading-screen art, its name a titled heading', async () => {
    document.documentElement.lang = 'en';

    const { host } = await visit('/en');

    const champion = host.querySelector('lodb-preview-section li') as HTMLElement;
    expect(champion.querySelector('img')?.getAttribute('src')).toBe(LOADING_ART);
    expect(champion.querySelector('h3')?.getAttribute('title')).toBe('First of champions');
    expect(champion.querySelector('h3 + p')?.textContent?.trim()).toBe('caption');
  });
});
