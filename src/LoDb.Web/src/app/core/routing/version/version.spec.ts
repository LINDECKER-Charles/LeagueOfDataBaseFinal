import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { type CanMatchFn, type Route, UrlSegment } from '@angular/router';
import { API_BASE_URL } from '../../api/api-base-url';
import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import { cacheClassOf } from './cache-class-of';
import { canMatchVersion } from './can-match-version';

const META: CatalogMeta = {
  latest: '16.19.1',
  versions: ['16.20.1', '16.19.1', '16.18.1', '15.14.1'],
  readyVersions: ['16.19.1'],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [{ locale: 'en', language: 'en_US' }],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};

describe('cacheClassOf', () => {
  it.each([
    ['16.19.1', 'latest'],
    ['16.18.1', 'archived'],
    ['15.14.1', 'archived'],
    ['16.20.1', 'latest'],
  ])('keeps version %s as %s', (version, expected) => {
    expect(cacheClassOf(version, META)).toBe(expected);
  });

  it('archives nothing before the first ingestion', () => {
    expect(cacheClassOf('15.14.1', { ...META, latest: null })).toBe('latest');
  });
});

describe('canMatchVersion', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function canMatch(...paths: string[]): Promise<boolean> {
    const segments = paths.map((path) => new UrlSegment(path, {}));
    const snapshot = {} as Parameters<CanMatchFn>[2];
    const verdict = TestBed.runInInjectionContext(() =>
      canMatchVersion({} as Route, segments, snapshot),
    );
    return verdict as Promise<boolean>;
  }

  it.each([
    [['15.14.1', 'champions'], true],
    [['99.99.1', 'champions'], true],
    [['16', 'champions'], false],
    [['about'], false],
    [['16.19.1-beta', 'items'], false],
  ])('matches %j by the pattern of /api/meta: %s', async (paths, expected) => {
    const verdict = canMatch(...paths);
    http.expectOne('/api/meta').flush(META);

    expect(await verdict).toBe(expected);
  });

  it('fails when /api/meta cannot answer, so the page answers 503 rather than 404', async () => {
    const verdict = canMatch('15.14.1', 'champions');
    http.expectOne('/api/meta').flush(null, { status: 503, statusText: 'Unavailable' });

    await expect(verdict).rejects.toMatchObject({ status: 503 });
  });
});
