import {
  HttpErrorResponse,
  type HttpInterceptorFn,
  HttpResponse,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import { Component, RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withRouterConfig } from '@angular/router';
import { of, throwError } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import type { HomeData } from '../data/home-data';
import { resolveHome } from './resolve-home';

@Component({ template: '' })
class Probe {}

const LATEST = '16.19.1';
const OLDER = '15.14.1';
const META: CatalogMeta = {
  latest: LATEST,
  versions: [LATEST, OLDER],
  readyVersions: [LATEST],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'en_GB', 'fr_FR'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
  ],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};
const IMAGE = { status: 'ready', url: '/cdn/blobs/abc.png', webpUrl: '/cdn/blobs/abc.webp' };
const LOADING_ART = 'https://ddragon.leagueoflegends.com/cdn/img/champion/loading/Aatrox_0.jpg';
const CHAMPIONS = {
  total: 171,
  entries: [
    {
      canonicalPath: 'champions/Aatrox',
      name: 'Aatrox',
      title: 'the Darkin Blade',
      image: IMAGE,
      loadingArt: LOADING_ART,
    },
  ],
};
const ITEMS = {
  total: 250,
  entries: [{ canonicalPath: 'items/1001-boots', id: '1001', name: 'Boots', image: IMAGE }],
};
const RUNES = {
  total: 63,
  entries: [],
  trees: [8000, 8100, 8200, 8300, 8400].map((id) => ({
    canonicalPath: `runes/${id}-path`,
    id,
    key: `Path${id}`,
    name: `Path ${id}`,
    image: { status: 'placeholder' },
  })),
};
const SUMMONERS = {
  total: 18,
  entries: [
    { canonicalPath: 'summoners/SummonerFlash', id: 'SummonerFlash', name: 'Flash', image: IMAGE },
  ],
};
const LISTS: Record<string, unknown> = {
  champions: CHAMPIONS,
  items: ITEMS,
  runes: RUNES,
  summoners: SUMMONERS,
};
const LIST_PATH = /^\/api\/catalog\/[^/]+\/[^/]+\/(\w+)\?page=1&size=4$/;

// The simulated API: /api/meta, then every list but the failing ones.
function simulatedApi(
  meta: CatalogMeta,
  failing: string[],
  requested: string[],
): HttpInterceptorFn {
  return (request) => {
    const url = request.urlWithParams;
    requested.push(url);
    const resource = LIST_PATH.exec(url)?.[1] ?? '';
    const body = url === '/api/meta' ? meta : LISTS[resource];
    if (body === undefined || failing.includes(resource)) {
      const status = HttpStatusCode.InternalServerError;
      return throwError(() => new HttpErrorResponse({ status, url }));
    }
    return of(new HttpResponse({ status: HttpStatusCode.Ok, url, body }));
  };
}

interface Options {
  readonly meta?: CatalogMeta;
  readonly failing?: string[];
}

async function visit(url: string, options: Options = {}) {
  const init: ResponseInit = { status: HttpStatusCode.Ok, headers: new Headers() };
  const requested: string[] = [];
  const api = simulatedApi(options.meta ?? META, options.failing ?? [], requested);
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          {
            path: ':locale',
            children: [{ path: '', resolve: { home: resolveHome }, component: Probe }],
          },
        ],
        withRouterConfig({ paramsInheritanceStrategy: 'always' }),
      ),
      provideLocationMocks(),
      provideHttpClient(withInterceptors([api])),
      { provide: API_BASE_URL, useValue: '' },
      { provide: RESPONSE_INIT, useValue: init },
    ],
  });
  const router = TestBed.inject(Router);
  await router.navigateByUrl(url);
  let leaf = router.routerState.snapshot.root;
  while (leaf.firstChild) {
    leaf = leaf.firstChild;
  }
  const cache = (init.headers as Headers).get('Cache-Control');
  return { home: leaf.data['home'] as HomeData, requested, cache };
}

describe('resolveHome', () => {
  it('reads the latest version in the locale language, cached as the latest', async () => {
    const { home, requested, cache } = await visit('/fr');

    expect(home.context).toEqual({
      locale: 'fr',
      version: LATEST,
      pinned: false,
      language: 'fr_FR',
    });
    expect(requested).toContain(`/api/catalog/${LATEST}/fr_FR/champions?page=1&size=4`);
    expect(cache).toBe('public, max-age=0, s-maxage=300, stale-while-revalidate=3600');
  });

  it('links each portal and card to its page in the latest version', async () => {
    const { home } = await visit('/fr');

    expect(home.sections.champions.list).toEqual({ path: '/fr/champions', query: {} });
    expect(home.sections.champions.total).toBe(171);
    expect(home.sections.champions.cards).toEqual([
      {
        link: { path: '/fr/champions/Aatrox', query: {} },
        name: 'Aatrox',
        caption: 'the Darkin Blade',
        image: '/cdn/blobs/abc.png',
        art: LOADING_ART,
      },
    ]);
    expect(home.sections.items.cards[0]?.caption).toBe('id 1001');
    expect(home.sections.summoners.cards[0]?.caption).toBe('SummonerFlash');
  });

  it('gives the champions alone their loading-screen art', async () => {
    const { home } = await visit('/en');

    expect(home.sections.champions.cards[0]?.art).toBe(LOADING_ART);
    expect(home.sections.items.cards[0]?.art).toBeNull();
    expect(home.sections.runes.cards[0]?.art).toBeNull();
    expect(home.sections.summoners.cards[0]?.art).toBeNull();
  });

  it('follows ?version= and ?lang=, and carries them into every link', async () => {
    const { home, requested, cache } = await visit(`/en?version=${OLDER}&lang=en_GB`);

    expect(home.context).toEqual({ locale: 'en', version: OLDER, pinned: true, language: 'en_GB' });
    expect(requested).toContain(`/api/catalog/${OLDER}/en_GB/items?page=1&size=4`);
    expect(home.sections.items.list).toEqual({
      path: `/en/${OLDER}/items`,
      query: { lang: 'en_GB' },
    });
    expect(home.sections.items.cards[0]?.link.path).toBe(`/en/${OLDER}/items/1001-boots`);
    expect(cache).toBe('public, max-age=3600, s-maxage=604800');
  });

  it('previews the first four rune paths and counts the paths, not their runes', async () => {
    const { home } = await visit('/en');

    expect(home.sections.runes.total).toBe(5);
    expect(home.sections.runes.cards.map((card) => card.name)).toEqual([
      'Path 8000',
      'Path 8100',
      'Path 8200',
      'Path 8300',
    ]);
    expect(home.sections.runes.cards[0]?.image).toBeNull();
  });

  it('empties the section of a list out of reach, the others still filled', async () => {
    const { home } = await visit('/en', { failing: ['items'] });

    expect(home.sections.items).toEqual({
      resource: 'items',
      list: { path: '/en/items', query: {} },
      total: null,
      cards: [],
    });
    expect(home.sections.champions.total).toBe(171);
  });

  it('keeps its portals and asks for no list while nothing is ingested', async () => {
    const meta = { ...META, latest: null, versions: [], readyVersions: [] };

    const { home, requested } = await visit('/fr', { meta });

    expect(home.context).toBeNull();
    expect(home.sections.runes).toEqual({
      resource: 'runes',
      list: { path: '/fr/runes', query: {} },
      total: null,
      cards: [],
    });
    expect(requested).toEqual(['/api/meta']);
  });
});
