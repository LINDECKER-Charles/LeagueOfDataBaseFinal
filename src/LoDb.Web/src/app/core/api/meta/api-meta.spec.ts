import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
  ɵwithHttpTransferCache as withHttpTransferCache,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { bypassTransferCacheInterceptor } from '../../http/interceptors/bypass-transfer-cache-interceptor';
import { catalogMetaOf } from '../../testing/catalog-meta-of';
import type { CatalogMeta } from '../generated/models/catalog-meta';
import { API_BASE_URL } from '../api-base-url';
import { ApiMeta } from './api-meta';

const ORIGIN = 'http://api:8080';
const META_URL = `${ORIGIN}/api/meta`;
const I18N_URL = '/i18n/en.json';
const META = catalogMetaOf({
  versions: ['16.19.1', '16.18.1'],
  languages: ['en_US', 'fr_FR', 'zh_CN'],
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
    { locale: 'zh-hans', language: 'zh_CN' },
  ],
});

// The document a page rendered weeks ago embeds: a patch has been promoted since.
const NEWER = { ...META, latest: '16.20.1', versions: ['16.20.1', ...META.versions] };

// The app's HTTP stack: its bypass interceptor, then Angular's transfer cache.
function setUp(platform = 'browser'): HttpTestingController {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([bypassTransferCacheInterceptor])),
      provideHttpClientTesting(),
      withHttpTransferCache({}),
      { provide: API_BASE_URL, useValue: ORIGIN },
      { provide: PLATFORM_ID, useValue: platform },
    ],
  });
  return TestBed.inject(HttpTestingController);
}

// The server render stores what it read in the page's `ng-state`: `ngServerMode` is a
// build-time flag of the server bundle, read when the request runs.
async function renderOnServer(http: HttpTestingController, url: string, body: object) {
  Reflect.set(globalThis, 'ngServerMode', true);
  try {
    const read = firstValueFrom(TestBed.inject(HttpClient).get(url));
    http.expectOne(url).flush(body);
    await read;
  } finally {
    Reflect.deleteProperty(globalThis, 'ngServerMode');
  }
}

// Every reader so far answered, and the app has had its chance to ask once more.
async function settled(): Promise<void> {
  await TestBed.inject(ApplicationRef).whenStable();
  await new Promise((resolve) => setTimeout(resolve));
}

function follow(meta: ApiMeta): CatalogMeta[] {
  const seen: CatalogMeta[] = [];
  meta.meta().subscribe((document) => seen.push(document));
  return seen;
}

describe('ApiMeta', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    http = setUp();
  });

  afterEach(() => http.verify());

  it('requests /api/meta on the origin of API_BASE_URL, once for every reader', async () => {
    const meta = TestBed.inject(ApiMeta);
    const language = firstValueFrom(meta.languageOf('fr'));
    const matcher = firstValueFrom(meta.versionMatcher());

    http.expectOne(`${ORIGIN}/api/meta`).flush(META);

    expect(await language).toBe('fr_FR');
    expect((await matcher).test('16.19.1')).toBe(true);
    expect(await firstValueFrom(meta.meta())).toEqual(META);
  });

  it('reads the default language for a locale the document does not map', async () => {
    const language = firstValueFrom(TestBed.inject(ApiMeta).languageOf('ja'));

    http.expectOne(`${ORIGIN}/api/meta`).flush(META);

    expect(await language).toBe('en_US');
  });

  it('matches whole versions only', async () => {
    const matcher = firstValueFrom(TestBed.inject(ApiMeta).versionMatcher());

    http.expectOne(`${ORIGIN}/api/meta`).flush(META);

    const matches = await matcher;
    expect(['7.21.1', '16.19.1', '0.151.2'].every((version) => matches.test(version))).toBe(true);
    expect(['16', '16.19.1-beta', 'v16.19.1', 'latest'].some((v) => matches.test(v))).toBe(false);
  });

  it('asks again after a failed request', async () => {
    const meta = TestBed.inject(ApiMeta);

    const failed = firstValueFrom(meta.meta());
    http.expectOne(`${ORIGIN}/api/meta`).flush(null, { status: 503, statusText: 'Unavailable' });
    await expect(failed).rejects.toBeTruthy();

    const retried = firstValueFrom(meta.meta());
    http.expectOne(`${ORIGIN}/api/meta`).flush(META);
    expect(await retried).toEqual(META);
  });

  it('asks once where the page embeds nothing, as the desktop and Android shells start', async () => {
    const seen = follow(TestBed.inject(ApiMeta));

    http.expectOne(META_URL).flush(META);
    await settled();

    http.expectNone(META_URL);
    expect(seen).toEqual([META]);
  });
});

// Stale while revalidate: the page's own answer for hydration, the network's once stable.
describe('ApiMeta on a server-rendered page', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    http = setUp();
    await renderOnServer(http, META_URL, META);
  });

  afterEach(() => http.verify());

  it('reads the embedded document first, then publishes the newer one once stable', async () => {
    const meta = TestBed.inject(ApiMeta);
    const seen = follow(meta);

    // Answered by the page, before anything reaches the network.
    expect(seen).toEqual([META]);
    const refresh = await vi.waitFor(() => http.expectOne(META_URL));
    expect(refresh.request.transferCache).toBe(false);
    refresh.flush(NEWER);

    expect(seen).toEqual([META, NEWER]);
    expect(await firstValueFrom(meta.meta())).toEqual(NEWER);
    await settled();
    http.expectNone(META_URL);
  });

  it('publishes nothing more when the network answers the same document', async () => {
    const seen = follow(TestBed.inject(ApiMeta));

    (await vi.waitFor(() => http.expectOne(META_URL))).flush(structuredClone(META));
    await settled();

    expect(seen).toEqual([META]);
    http.expectNone(META_URL);
  });

  it('keeps the embedded document when the refresh fails, and asks no more', async () => {
    const meta = TestBed.inject(ApiMeta);
    const seen = follow(meta);

    const refresh = await vi.waitFor(() => http.expectOne(META_URL));
    refresh.flush(null, { status: 503, statusText: 'Unavailable' });
    await settled();

    expect(seen).toEqual([META]);
    expect(await firstValueFrom(meta.meta())).toEqual(META);
    http.expectNone(META_URL);
  });
});

describe('ApiMeta on a page that embeds no document', () => {
  let http: HttpTestingController;

  afterEach(() => http.verify());

  it('asks once on a prerendered page, whose state holds other answers', async () => {
    http = setUp();
    await renderOnServer(http, I18N_URL, {});
    const seen = follow(TestBed.inject(ApiMeta));

    http.expectOne(META_URL).flush(NEWER);
    await settled();

    http.expectNone(META_URL);
    expect(seen).toEqual([NEWER]);
  });

  it('asks once on the server, which renders what it read', async () => {
    http = setUp('server');
    const seen = follow(TestBed.inject(ApiMeta));

    http.expectOne(META_URL).flush(META);
    await settled();

    http.expectNone(META_URL);
    expect(seen).toEqual([META]);
  });
});
