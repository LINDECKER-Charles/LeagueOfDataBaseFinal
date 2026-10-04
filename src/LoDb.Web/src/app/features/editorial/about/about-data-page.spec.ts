import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { NEVER, of } from 'rxjs';
import { activateLocale } from '../../../core/i18n/activate-locale';
import { LOCALES } from '../../../core/i18n/locales';
import { CANONICAL_ORIGIN } from '../../../core/seo/canonical-origin';
import { EDITORIAL_ROUTES } from '../editorial.routes';
import { DATA_DRAGON_LANGUAGES } from './data-dragon-languages';
import { Inventory } from './inventory/inventory';

const ORIGIN = 'https://league-of-data-base.com';
const CATALOGUES: Record<string, Translation> = {
  'editorial/fr': { data: { dataset_name: 'Données de LoDb' } },
};
const BCP_47_REGIONAL = /^[a-z]{2}-[A-Z]{2}$/;

interface JsonLdNode {
  readonly '@type': string;
  readonly [field: string]: unknown;
}

async function visit(url: string): Promise<void> {
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: ORIGIN },
      // The counts load in the browser; the head does not wait for them.
      { provide: Inventory, useValue: { snapshot: () => NEVER } },
      provideRouter([
        { path: ':locale', resolve: { locale: activateLocale }, children: EDITORIAL_ROUTES },
      ]),
      provideTransloco({
        config: {
          availableLangs: [...LOCALES],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = (path: string) => of(CATALOGUES[path] ?? {});
        },
      }),
    ],
  });
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
}

function datasetNode(): JsonLdNode | undefined {
  const scripts = document.head.querySelectorAll('script[type="application/ld+json"]');
  const nodes = [...scripts].map((script) => JSON.parse(script.textContent ?? '') as JsonLdNode);
  return nodes.find((node) => node['@type'] === 'Dataset');
}

describe('DATA_DRAGON_LANGUAGES', () => {
  it('lists the 28 languages of Data Dragon once each, in its order', () => {
    expect(DATA_DRAGON_LANGUAGES).toHaveLength(28);
    expect(new Set(DATA_DRAGON_LANGUAGES).size).toBe(28);
    expect(DATA_DRAGON_LANGUAGES.slice(0, 3)).toEqual(['ar-AE', 'en-US', 'cs-CZ']);
    expect(DATA_DRAGON_LANGUAGES.at(-1)).toBe('zh-TW');
  });

  it('writes each language as a BCP 47 tag, never as a Data Dragon code', () => {
    for (const language of DATA_DRAGON_LANGUAGES) {
      expect(language).toMatch(BCP_47_REGIONAL);
    }
    expect(DATA_DRAGON_LANGUAGES).toContain('fr-FR');
    expect(DATA_DRAGON_LANGUAGES).not.toContain('fr_FR');
  });
});

describe('AboutDataPage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    document.head.querySelectorAll('[data-lodb-seo]').forEach((element) => element.remove());
  });

  it('describes the data in the languages of Data Dragon, not in the site locales', async () => {
    await visit('/fr/about/data');
    const dataset = datasetNode();

    expect(dataset?.['inLanguage']).toEqual(DATA_DRAGON_LANGUAGES);
    expect(dataset?.['inLanguage']).not.toContain('zh-hans');
  });

  it('keeps the rest of the Dataset node: its address, name and creator', async () => {
    await visit('/fr/about/data');

    expect(datasetNode()).toMatchObject({
      '@id': `${ORIGIN}/fr/about/data#dataset`,
      url: `${ORIGIN}/fr/about/data`,
      name: 'Données de LoDb',
      creator: { '@id': `${ORIGIN}/#organization` },
      isAccessibleForFree: true,
    });
  });
});
