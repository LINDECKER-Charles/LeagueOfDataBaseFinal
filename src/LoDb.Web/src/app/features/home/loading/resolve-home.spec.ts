import {
  HttpErrorResponse,
  HttpHeaders,
  type HttpInterceptorFn,
  HttpResponse,
  HttpStatusCode,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import {
  Component,
  Injector,
  PLATFORM_ID,
  RESPONSE_INIT,
  TransferState,
  runInInjectionContext,
  signal,
} from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withRouterConfig } from '@angular/router';
import { of, throwError } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import type { HomeData } from '../data/home-data';
import type { HomeSection } from '../data/home-section';
import { injectRefreshedSections } from './inject-refreshed-sections';
import { injectRetryTransfer } from './inject-retry-transfer';
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
    {
      canonicalPath: 'summoners/SummonerBarrier_Jade',
      id: 'SummonerBarrier_Jade',
      name: 'Barrier',
      image: IMAGE,
      edition: 'classic',
    },
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
interface Options {
  readonly meta?: CatalogMeta;
  readonly failing?: string[];
  /** `Retry-After` of the lists whose version still fetches images, by resource. */
  readonly retryAfter?: Readonly<Record<string, string>>;
  /** Lists whose previewed images are still pending. */
  readonly pending?: string[];
}

// A list whose images are all pending, as a cold version answers before its art is stored.
function withPendingImages(body: unknown): unknown {
  const list = body as { entries: object[]; trees?: object[] };
  const pending = (entries: object[]) =>
    entries.map((one) => ({ ...one, image: { status: 'pending' } }));
  return { ...list, entries: pending(list.entries), trees: list.trees && pending(list.trees) };
}

// A list as the API answers it, pending images and `Retry-After` included when asked.
function answerOf(options: Options, url: string, body: unknown) {
  const resource = LIST_PATH.exec(url)?.[1] ?? '';
  const delay = options.retryAfter?.[resource];
  const headers = new HttpHeaders(delay === undefined ? {} : { 'Retry-After': delay });
  const answer = options.pending?.includes(resource) ? withPendingImages(body) : body;
  return new HttpResponse({ status: HttpStatusCode.Ok, url, body: answer, headers });
}

function simulatedApi(options: Options, requested: string[]): HttpInterceptorFn {
  return (request) => {
    const url = request.urlWithParams;
    requested.push(url);
    const resource = LIST_PATH.exec(url)?.[1] ?? '';
    const body = url === '/api/meta' ? (options.meta ?? META) : LISTS[resource];
    if (body === undefined || (options.failing ?? []).includes(resource)) {
      const status = HttpStatusCode.InternalServerError;
      return throwError(() => new HttpErrorResponse({ status, url }));
    }
    return of(answerOf(options, url, body));
  };
}

async function visit(url: string, options: Options = {}) {
  const init: ResponseInit = { status: HttpStatusCode.Ok, headers: new Headers() };
  const requested: string[] = [];
  const api = simulatedApi(options, requested);
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
        image: IMAGE,
        edition: 'modern',
        art: LOADING_ART,
      },
    ]);
    expect(home.sections.items.cards[0]?.caption).toBe('id 1001');
    expect(home.sections.summoners.cards[0]).toMatchObject({
      caption: 'SummonerBarrier_Jade',
      edition: 'classic',
    });
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
    expect(home.sections.runes.cards[0]?.image).toEqual({ status: 'placeholder' });
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

  it('keeps a home whose images are on their way out of shared caches', async () => {
    const retryAfter = { champions: '9', items: '5', runes: '2' };
    const { home, cache } = await visit('/en', { retryAfter, pending: ['items', 'runes'] });

    // The champions list asks too, but its preview shows no pending image.
    expect(home.retryAfterMs).toBe(5000);
    expect(cache).toBe('public, max-age=0, s-maxage=60');
  });

  it('reads a list asking for a retry as settled when its preview shows no pending image', async () => {
    const { home, cache } = await visit('/fr', { retryAfter: { items: '5' } });

    expect(home.retryAfterMs).toBeNull();
    expect(cache).toBe('public, max-age=0, s-maxage=300, stale-while-revalidate=3600');
  });
});

describe('injectRefreshedSections', () => {
  afterEach(() => vi.useRealTimers());

  it('reads pending previews once more, after the delay the API asked for', async () => {
    vi.useFakeTimers();
    const requested: string[] = [];
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([simulatedApi({}, requested)])),
        { provide: API_BASE_URL, useValue: '' },
      ],
    });
    const context = { locale: 'en', version: LATEST, pinned: false, language: 'en_US' } as const;
    const stale: HomeSection = {
      resource: 'items',
      list: { path: '/en/items', query: {} },
      total: 0,
      cards: [],
    };
    const sections = { champions: stale, items: stale, runes: stale, summoners: stale } as const;
    const data = signal<HomeData>({ context, sections, retryAfterMs: 5000 });
    const refreshed = TestBed.runInInjectionContext(() => injectRefreshedSections(data));
    TestBed.tick();

    expect(refreshed().items.cards).toEqual([]);
    await vi.advanceTimersByTimeAsync(5000);
    expect(refreshed().items.cards.map((card) => card.name)).toEqual(['Boots']);
    await vi.advanceTimersByTimeAsync(60_000);
    expect(requested.filter((url) => url.includes('/items?'))).toHaveLength(1);
  });
});

describe('injectRetryTransfer', () => {
  it('hands the delay the server read to the first browser read that lacks one', () => {
    const state = new TransferState();
    const on = (platform: string) =>
      runInInjectionContext(
        Injector.create({
          providers: [
            { provide: PLATFORM_ID, useValue: platform },
            { provide: TransferState, useValue: state },
          ],
        }),
        injectRetryTransfer,
      );

    expect(on('server')(5000)).toBe(5000);
    const inBrowser = on('browser');
    expect(inBrowser(null)).toBe(5000);
    expect(inBrowser(null)).toBeNull();
    expect(inBrowser(2000)).toBe(2000);
  });
});
