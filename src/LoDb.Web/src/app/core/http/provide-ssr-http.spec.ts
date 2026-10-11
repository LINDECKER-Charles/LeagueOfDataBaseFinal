import {
  HTTP_TRANSFER_CACHE_ORIGIN_MAP,
  HttpBackend,
  HttpClient,
  provideHttpClient,
} from '@angular/common/http';
import { REQUEST, REQUEST_CONTEXT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../api/api-base-url';
import { ApiResponseMemo } from './api-response-memo';
import { provideSsrHttp } from './provide-ssr-http';
import { SsrHttpBackend } from './ssr-http-backend';
import type { SsrRequestContext } from './ssr-request-context';

const PAGE = 'https://league-of-data-base.com';
const API = 'http://api:8080';
const META_TTL_MS = 60_000;
const CATALOGUE = Object.freeze({ title: 'Objets' });

describe('provideSsrHttp', () => {
  let fetch: ReturnType<typeof vi.fn>;
  let now: number;

  function context(buildFiles: SsrRequestContext['buildFiles']): SsrRequestContext {
    return {
      apiOrigin: API,
      buildFiles,
      apiResponses: new ApiResponseMemo(new Set([`${API}/api/meta`]), META_TTL_MS, () => now),
    };
  }

  function configure(value: SsrRequestContext | null, request: Request | null): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideSsrHttp(),
        { provide: REQUEST, useValue: request },
        { provide: REQUEST_CONTEXT, useValue: value },
      ],
    });
  }

  const get = (url: string) => firstValueFrom(TestBed.inject(HttpClient).get(url));
  const fetchedUrls = () => fetch.mock.calls.map(([url]) => url as string);

  beforeEach(() => {
    now = 0;
    fetch = vi.fn(() => Promise.resolve(Response.json({ answered: true })));
    vi.stubGlobal('fetch', fetch);
  });

  afterEach(() => vi.unstubAllGlobals());

  it('calls the API on its internal origin', () => {
    configure(context(null), new Request(`${PAGE}/fr/`));

    expect(TestBed.inject(API_BASE_URL)).toBe(API);
  });

  it('keys API responses under the public origin the browser will request', () => {
    configure(context(null), new Request(`${PAGE}/fr/`));

    expect(TestBed.inject(HTTP_TRANSFER_CACHE_ORIGIN_MAP)).toEqual({ [API]: PAGE });
  });

  it("answers the page's own files from the build files, without a request", async () => {
    configure(context(new Map([['i18n/fr.json', CATALOGUE]])), new Request(`${PAGE}/fr/`));

    expect(TestBed.inject(HttpBackend)).toBeInstanceOf(SsrHttpBackend);
    await expect(get(`${PAGE}/i18n/fr.json`)).resolves.toBe(CATALOGUE);
    await expect(get(`${PAGE}/i18n/zz.json`)).rejects.toMatchObject({ status: 404 });
    expect(fetch).not.toHaveBeenCalled();
  });

  it('leaves the API and every other origin to fetch', async () => {
    configure(context(new Map()), new Request(`${PAGE}/fr/`));

    await get(`${API}/api/catalog/16.1.1/fr_FR/items`);
    await get('https://ddragon.leagueoflegends.com/api/versions.json');

    expect(fetchedUrls()).toEqual([
      `${API}/api/catalog/16.1.1/fr_FR/items`,
      'https://ddragon.leagueoflegends.com/api/versions.json',
    ]);
  });

  it("leaves the page's files to the host server when it holds none (ng serve)", async () => {
    configure(context(null), new Request('http://localhost:4200/fr/'));

    await get('http://localhost:4200/i18n/fr.json');

    expect(fetchedUrls()).toEqual(['http://localhost:4200/i18n/fr.json']);
  });

  it('asks the API for /api/meta once a minute, and serves its last copy when it fails', async () => {
    configure(context(new Map()), new Request(`${PAGE}/fr/`));

    await get(`${API}/api/meta`);
    now = META_TTL_MS - 1;
    await get(`${API}/api/meta`);
    expect(fetch).toHaveBeenCalledOnce();

    now = META_TTL_MS;
    fetch.mockRejectedValueOnce(new TypeError('fetch failed'));
    await expect(get(`${API}/api/meta`)).resolves.toEqual({ answered: true });
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('degrades to relative API URLs outside a request (route extraction)', () => {
    configure(null, null);

    expect(TestBed.inject(API_BASE_URL)).toBe('');
    expect(TestBed.inject(HTTP_TRANSFER_CACHE_ORIGIN_MAP)).toEqual({});
  });
});
