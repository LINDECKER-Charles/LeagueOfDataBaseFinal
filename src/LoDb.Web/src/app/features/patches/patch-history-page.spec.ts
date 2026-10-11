import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { CatalogMeta } from '../../core/api/generated/models/catalog-meta';
import { ApiMeta } from '../../core/api/meta/api-meta';
import { activateLocale } from '../../core/i18n/activate-locale';
import { LOCALES } from '../../core/i18n/locales';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Seo } from '../../core/seo/seo';
import { PATCHES_CATALOGUES } from '../../core/testing/patches-catalogues';
import { ROOT_CATALOGUES } from '../../core/testing/root-catalogues';
import { PATCHES_ROUTES } from './patches.routes';
import { versionGroupsOf } from './rows/version-groups-of';

const SITE = 'https://league-of-data-base.com';
const LATEST = '16.19.1';
const VERSIONS = [LATEST, '16.18.1', '15.24.1', '3.6.14', '0.151.2'];
const LISTS = ['champions', 'items', 'runes', 'summoners'];
const CATALOGUES: Record<string, Translation> = {
  fr: ROOT_CATALOGUES.fr,
  'patches/fr': PATCHES_CATALOGUES.fr,
};

// Only what the page reads of `/api/meta`.
function metaOf(latest: string | null, versions: readonly string[]): Partial<CatalogMeta> {
  return { latest, versions: [...versions] };
}

async function visit(meta: Partial<CatalogMeta>) {
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: SITE },
      { provide: ApiMeta, useValue: { meta: () => of(meta) } },
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([
        {
          path: ':locale',
          resolve: { locale: activateLocale },
          children: [{ path: 'patches', children: PATCHES_ROUTES }],
        },
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
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const harness = await RouterTestingHarness.create('/fr/patches');
  await harness.fixture.whenStable();
  return { page: harness.routeNativeElement as HTMLElement, apply };
}

function hrefsOf(row: Element | null | undefined): (string | null)[] {
  return Array.from(row?.querySelectorAll('a') ?? [], (link) => link.getAttribute('href'));
}

// A dotted path into a catalogue: `footer.navigation.patches`.
function valueAt(catalogue: Translation, key: string): unknown {
  return key.split('.').reduce<unknown>((node, part) => {
    return typeof node === 'object' && node !== null
      ? (node as Record<string, unknown>)[part]
      : undefined;
  }, catalogue);
}

describe('PatchHistoryPage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  // No request at all: the page reads `/api/meta`, shared with the render, and nothing else.
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    document.head.querySelectorAll('[data-lodb-seo]').forEach((element) => element.remove());
  });

  it('groups every listed version by first segment, newest first', async () => {
    const { page } = await visit(metaOf(LATEST, VERSIONS));

    const headings = Array.from(page.querySelectorAll('h2'), (h2) => h2.textContent?.trim());
    const versions = Array.from(page.querySelectorAll('tbody th'), (th) =>
      th.firstChild?.textContent?.trim(),
    );
    expect(headings).toEqual(['Patchs 16.x', 'Patchs 15.x', 'Patchs 3.x', 'Patchs 0.x']);
    expect(versions).toEqual(VERSIONS);
  });

  it('links the lists of the latest version by their short URLs, the others pinned', async () => {
    const { page } = await visit(metaOf(LATEST, VERSIONS));
    const rows = page.querySelectorAll('tbody tr');

    expect(hrefsOf(rows[0])).toEqual(LISTS.map((list) => `/fr/${list}`));
    expect(hrefsOf(rows[4])).toEqual(LISTS.map((list) => `/fr/0.151.2/${list}`));
    expect(rows[0]?.querySelector('th .hx-chip-hex')?.textContent?.trim()).toBe('Actuel');
    expect(rows[1]?.querySelector('th .hx-chip-hex')).toBeNull();
  });

  it('names each link by its list, in the row headed by its version', async () => {
    const { page } = await visit(metaOf(LATEST, VERSIONS));
    const row = page.querySelectorAll('tbody tr')[1];
    const links = Array.from(row?.querySelectorAll('td a') ?? []);

    expect(row?.querySelector('th')?.getAttribute('scope')).toBe('row');
    expect(row?.querySelector('th')?.textContent?.trim()).toBe('16.18.1');
    expect(links.map((link) => link.textContent?.trim())).toEqual([
      'Champions',
      'Objets',
      'Runes',
      'Sorts d’invocateur',
    ]);
  });

  it('pins every version before the first promotion', async () => {
    const { page } = await visit(metaOf(null, VERSIONS));

    expect(hrefsOf(page.querySelector('tbody tr'))).toEqual(
      LISTS.map((list) => `/fr/${LATEST}/${list}`),
    );
    expect(page.querySelector('th .hx-chip-hex')).toBeNull();
  });

  it('says so when Data Dragon lists no version', async () => {
    const { page } = await visit(metaOf(null, []));

    expect(page.querySelector('table')).toBeNull();
    expect(page.textContent).toContain('Aucun patch n’est listé pour l’instant.');
  });

  it('writes an indexable head, canonical on its own path, with its breadcrumb', async () => {
    const { apply } = await visit(metaOf(LATEST, VERSIONS));

    const head = apply.mock.lastCall![0];
    expect(head).toMatchObject({
      title: 'Historique des patchs',
      description: PATCHES_CATALOGUES.fr['description'],
      path: 'patches',
    });
    expect(head).not.toHaveProperty('kind');
    expect(head).not.toHaveProperty('version');
  });
});

describe('versionGroupsOf', () => {
  it('keeps a version listed out of order in the group of its first segment', () => {
    expect(versionGroupsOf(['16.2.1', '15.1.1', '16.1.1'])).toEqual([
      { major: '16', versions: ['16.2.1', '16.1.1'] },
      { major: '15', versions: ['15.1.1'] },
    ]);
  });
});

describe('texts of the patch history', () => {
  const keys = Object.keys(PATCHES_CATALOGUES.en);

  it.each(LOCALES)('are all translated in %s', (locale) => {
    const catalogue = PATCHES_CATALOGUES[locale];

    expect(Object.keys(catalogue)).toEqual(keys);
    for (const key of keys) {
      expect(String(catalogue[key]).trim(), `${locale}: ${key}`).not.toBe('');
    }
    expect(catalogue['group']).toContain('{{ major }}');
    expect(valueAt(ROOT_CATALOGUES[locale], 'footer.navigation.patches')).toMatch(/\S/);
  });
});
