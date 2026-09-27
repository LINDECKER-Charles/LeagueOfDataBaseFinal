import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { type Translation, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import EN from '../../../../public/i18n/en.json';
import FR from '../../../../public/i18n/fr.json';
import { provideI18n } from './provide-i18n';

// Shaped like the converted catalogues (tools/next/i18n): `{{ }}` parameters.
const CATALOGUES: Record<string, Translation> = {
  'i18n/en.json': {
    filter: { page: 'page {{ page }} / {{ count }}' },
    only_in_en: 'Only in English',
  },
  'i18n/fr.json': { filter: { page: 'page {{ page }} sur {{ count }}' } },
  'i18n/ja.json': { filter: { page: '{{ page }} / {{ count }} ページ' } },
  'i18n/seo/en.json': { title: 'Champions', suffix: '(patch {{ version }})' },
  'i18n/seo/fr.json': { title: 'Champions FR' },
};
// The shipped root catalogues, for the plurals, whose shape is what is under test. Japanese
// has no count message of its own: its counts are the `en` ones.
const SHIPPED: Record<string, Translation> = {
  'i18n/en.json': EN,
  'i18n/fr.json': FR,
  'i18n/ja.json': {},
};

describe('provideI18n', () => {
  let transloco: TranslocoService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideI18n()],
    });
    transloco = TestBed.inject(TranslocoService);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
  });

  afterEach(() => vi.restoreAllMocks());

  async function load(path: string, catalogues = CATALOGUES): Promise<void> {
    const loaded = firstValueFrom(transloco.load(path));
    const http = TestBed.inject(HttpTestingController);
    for (const request of http.match(() => true)) {
      request.flush(catalogues[request.request.url]);
    }
    await loaded;
    http.verify();
  }

  const counts = (key: string) =>
    [0, 1, 2].map((count) => transloco.translate(`filter.${key}`, { count }));

  it('falls back to en for a key the locale lacks, and only for that key', async () => {
    await load('fr');
    transloco.setActiveLang('fr');

    expect(transloco.translate('filter.page', { page: 2, count: 5 })).toBe('page 2 sur 5');
    expect(transloco.translate('only_in_en')).toBe('Only in English');
  });

  it('falls back to en key by key inside a scope too', async () => {
    await load('fr');
    await load('seo/fr');
    transloco.setActiveLang('fr');

    expect(transloco.translate('seo.title')).toBe('Champions FR');
    expect(transloco.translate('seo.suffix', { version: '16.1' })).toBe('(patch 16.1)');
  });

  // The legacy lists took their singular for a count of exactly one, whatever the locale:
  // French reads "0 résultats", and the `en` fallback of Japanese, which has no `one`
  // category, still reads "1 result".
  it.each([
    [
      'en',
      ['0 results', '1 result', '2 results'],
      ['Show 0 results', 'Show 1 result', 'Show 2 results'],
    ],
    [
      'fr',
      ['0 résultats', '1 résultat', '2 résultats'],
      ['Voir 0 résultats', 'Voir 1 résultat', 'Voir 2 résultats'],
    ],
    [
      'ja',
      ['0 results', '1 result', '2 results'],
      ['Show 0 results', 'Show 1 result', 'Show 2 results'],
    ],
  ])(
    'counts the list results in %s with the singular for one exactly',
    async (locale, results, show) => {
      await load(locale, SHIPPED);
      transloco.setActiveLang(locale);

      expect(counts('results')).toEqual(results);
      expect(counts('show_results')).toEqual(show);
    },
  );

  it('formats the parameters of an en message under a locale of its own', async () => {
    await load('ja');
    transloco.setActiveLang('ja');

    expect(transloco.translate('filter.page', { page: 1, count: 3 })).toBe('1 / 3 ページ');
  });
});
