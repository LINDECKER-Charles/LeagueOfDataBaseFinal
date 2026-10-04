import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../api-base-url';
import type { CatalogMeta } from '../generated/models/catalog-meta';
import { ApiMeta } from './api-meta';

const ORIGIN = 'http://api:8080';
const META: CatalogMeta = {
  latest: '16.19.1',
  versions: ['16.19.1', '16.18.1'],
  readyVersions: ['16.19.1'],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'fr_FR', 'zh_CN'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
    { locale: 'zh-hans', language: 'zh_CN' },
  ],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};

describe('ApiMeta', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: ORIGIN },
      ],
    });
    http = TestBed.inject(HttpTestingController);
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
});
