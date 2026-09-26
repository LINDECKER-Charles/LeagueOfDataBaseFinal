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
import { provideSsrHttp } from './provide-ssr-http';
import { SelfOriginFetchBackend } from './self-origin-fetch-backend';
import type { SsrRequestContext } from './ssr-request-context';

const PAGE = 'https://league-of-data-base.com';
const CONTEXT: SsrRequestContext = {
  apiOrigin: 'http://api:8080',
  selfOrigin: 'http://127.0.0.1:4000',
};

describe('provideSsrHttp', () => {
  let fetch: ReturnType<typeof vi.fn>;

  function configure(context: SsrRequestContext | null, request: Request | null): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideSsrHttp(),
        { provide: REQUEST, useValue: request },
        { provide: REQUEST_CONTEXT, useValue: context },
      ],
    });
  }

  function fetchedUrl(url: string): Promise<string> {
    return firstValueFrom(TestBed.inject(HttpClient).get(url)).then(
      () => fetch.mock.calls[0][0] as string,
    );
  }

  beforeEach(() => {
    fetch = vi.fn(() => Promise.resolve(Response.json({})));
    vi.stubGlobal('fetch', fetch);
  });

  afterEach(() => vi.unstubAllGlobals());

  it('calls the API on its internal origin', () => {
    configure(CONTEXT, new Request(`${PAGE}/fr/`));

    expect(TestBed.inject(API_BASE_URL)).toBe('http://api:8080');
  });

  it('keys API responses under the public origin the browser will request', () => {
    configure(CONTEXT, new Request(`${PAGE}/fr/`));

    expect(TestBed.inject(HTTP_TRANSFER_CACHE_ORIGIN_MAP)).toEqual({ 'http://api:8080': PAGE });
  });

  it('fetches same-origin files from the SSR server itself', async () => {
    configure(CONTEXT, new Request(`${PAGE}/fr/`));

    expect(TestBed.inject(HttpBackend)).toBeInstanceOf(SelfOriginFetchBackend);
    await expect(fetchedUrl(`${PAGE}/i18n/fr.json`)).resolves.toBe(
      'http://127.0.0.1:4000/i18n/fr.json',
    );
  });

  it('leaves API requests untouched', async () => {
    configure(CONTEXT, new Request(`${PAGE}/fr/`));

    await expect(fetchedUrl('http://api:8080/api/meta')).resolves.toBe('http://api:8080/api/meta');
  });

  it('leaves same-origin files to the host server when it does not listen itself', async () => {
    configure({ ...CONTEXT, selfOrigin: null }, new Request('http://localhost:4200/fr/'));

    await expect(fetchedUrl('http://localhost:4200/i18n/fr.json')).resolves.toBe(
      'http://localhost:4200/i18n/fr.json',
    );
  });

  it('degrades to relative API URLs outside a request (route extraction)', () => {
    configure(null, null);

    expect(TestBed.inject(API_BASE_URL)).toBe('');
    expect(TestBed.inject(HTTP_TRANSFER_CACHE_ORIGIN_MAP)).toEqual({});
  });
});
