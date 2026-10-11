import { TestBed } from '@angular/core/testing';
import {
  TRANSLOCO_TRANSPILER,
  type Translation,
  TranslocoService,
  provideTransloco,
} from '@jsverse/transloco';
import { firstValueFrom, of } from 'rxjs';
import { PrecompiledTranspiler } from './precompiled-transpiler';

// Built catalogues (npm run i18n:build): ICU messages come compiled, the rest as written.
const CATALOGUES: Record<string, Translation> = {
  en: {
    plain: 'Items',
    greeting: 'Hello {{ name }}',
    alias: '{{ plain }} list',
    results: [
      [
        'plural',
        'count',
        0,
        { 1: [['#', 'count', 0], ' result'], other: [['#', 'count', 0], ' results'] },
      ],
    ],
    rank: ['#', ['arg', 'rank'], ' — #1'],
    place: [
      [
        'selectordinal',
        'n',
        0,
        {
          one: [['#', 'n', 0], 'st'],
          two: [['#', 'n', 0], 'nd'],
          few: [['#', 'n', 0], 'rd'],
          other: [['#', 'n', 0], 'th'],
        },
      ],
    ],
    guests: [
      [
        'plural',
        'count',
        1,
        {
          0: ['nobody'],
          1: ['only ', ['arg', 'host']],
          other: [['arg', 'host'], ' and ', ['#', 'count', 1], ' others'],
        },
      ],
    ],
    kind: [['select', 'kind', { item: ['an item'], other: ['a ', ['arg', 'kind']] }]],
  },
  fr: {
    results: [
      [
        'plural',
        'count',
        0,
        { one: [['#', 'count', 0], ' résultat'], other: [['#', 'count', 0], ' résultats'] },
      ],
    ],
  },
};

describe('PrecompiledTranspiler', () => {
  let transloco: TranslocoService;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideTransloco({
          config: {
            availableLangs: ['en', 'fr'],
            defaultLang: 'en',
            missingHandler: { logMissingKey: false },
            prodMode: true,
          },
          loader: class {
            getTranslation = (lang: string) => of(CATALOGUES[lang]);
          },
        }),
        { provide: TRANSLOCO_TRANSPILER, useClass: PrecompiledTranspiler },
      ],
    });
    transloco = TestBed.inject(TranslocoService);
    await Promise.all(['en', 'fr'].map((lang) => firstValueFrom(transloco.load(lang))));
  });

  it('leaves a plain message to Transloco, parameters and key references included', () => {
    expect(transloco.translate('plain')).toBe('Items');
    expect(transloco.translate('greeting', { name: "Kai'Sa {x}" })).toBe("Hello Kai'Sa {x}");
    expect(transloco.translate('alias')).toBe('Items list');
  });

  it('takes an exact case before the category, and formats # for the locale', () => {
    expect([0, 1, 2, 1234].map((count) => transloco.translate('results', { count }))).toEqual([
      '0 results',
      '1 result',
      '2 results',
      '1,234 results',
    ]);
  });

  it('follows the plural rules and the numbers of the active language', () => {
    transloco.setActiveLang('fr');

    expect([0, 1, 2, 1234].map((count) => transloco.translate('results', { count }))).toEqual([
      '0 résultat',
      '1 résultat',
      '2 résultats',
      '1 234 résultats',
    ]);
  });

  it('renders arguments as they are and # outside a plural as text', () => {
    expect(transloco.translate('rank', { rank: 3 })).toBe('#3 — #1');
  });

  it('renders ordinals, offsets and selects', () => {
    expect([1, 2, 3, 4, 11].map((n) => transloco.translate('place', { n }))).toEqual([
      '1st',
      '2nd',
      '3rd',
      '4th',
      '11th',
    ]);
    expect(
      [0, 1, 3].map((count) => transloco.translate('guests', { count, host: 'Ahri' })),
    ).toEqual(['nobody', 'only Ahri', 'Ahri and 2 others']);
    expect(transloco.translate('kind', { kind: 'item' })).toBe('an item');
    expect(transloco.translate('kind', { kind: 'rune' })).toBe('a rune');
  });
});
