import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import type { Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { CatalogMeta } from '../../core/api/generated/models/catalog-meta';
import { ApiMeta } from '../../core/api/meta/api-meta';
import { activateLocale } from '../../core/i18n/activate-locale';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Seo } from '../../core/seo/seo';
import { PATCHES_CATALOGUES } from '../../core/testing/patches-catalogues';
import { provideTestTransloco } from '../../core/testing/provide-test-transloco';
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

// The oldest version with a page of changes: every one from it up to the newest has one, the
// latest unless the index lags behind its promotion.
const OLDEST_CHANGES = '15.24.1';

// Only what the page reads of `/api/meta`, over an index that covers the latest version.
function metaOf(
  latest: string | null,
  versions: readonly string[],
  oldestChangesVersion: string | null = OLDEST_CHANGES,
): Partial<CatalogMeta> {
  const newestChangesVersion = oldestChangesVersion && latest;
  return { latest, versions: [...versions], oldestChangesVersion, newestChangesVersion };
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
      provideTestTransloco({ byPath: CATALOGUES }),
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

    expect(hrefsOf(rows[0])).toEqual(['/fr/changes', ...LISTS.map((list) => `/fr/${list}`)]);
    expect(hrefsOf(rows[4])).toEqual(LISTS.map((list) => `/fr/0.151.2/${list}`));
    expect(rows[0]?.querySelector('th .hx-chip-hex')?.textContent?.trim()).toBe('Actuel');
    expect(rows[1]?.querySelector('th .hx-chip-hex')).toBeNull();
  });

  it('links the changes of each version that has a page of them first', async () => {
    const { page } = await visit(metaOf(LATEST, VERSIONS));
    const firsts = Array.from(page.querySelectorAll('tbody tr'), (row) =>
      row.querySelector('td a')?.getAttribute('href'),
    );

    expect(firsts).toEqual([
      '/fr/changes',
      '/fr/16.18.1/changes',
      '/fr/15.24.1/changes',
      '/fr/3.6.14/champions',
      '/fr/0.151.2/champions',
    ]);
  });

  it('links no page of changes for a version listed after the latest', async () => {
    const { page } = await visit(metaOf(LATEST, ['16.20.1', ...VERSIONS]));

    expect(hrefsOf(page.querySelector('tbody tr'))).toEqual(
      LISTS.map((list) => `/fr/16.20.1/${list}`),
    );
  });

  it('links no page of changes past the end of an index left behind the promotion', async () => {
    const { page } = await visit({ ...metaOf(LATEST, VERSIONS), newestChangesVersion: '16.18.1' });
    const firsts = Array.from(page.querySelectorAll('tbody tr'), (row) =>
      row.querySelector('td a')?.getAttribute('href'),
    );

    expect(firsts.slice(0, 2)).toEqual(['/fr/champions', '/fr/16.18.1/changes']);
  });

  it('links no page of changes while the index covers fewer than two versions', async () => {
    const { page } = await visit(metaOf(LATEST, VERSIONS, null));

    expect(hrefsOf(page.querySelector('tbody tr'))).toEqual(LISTS.map((list) => `/fr/${list}`));
  });

  it('names each link by its list, in the row headed by its version', async () => {
    const { page } = await visit(metaOf(LATEST, VERSIONS));
    const row = page.querySelectorAll('tbody tr')[1];
    const links = Array.from(row?.querySelectorAll('td a') ?? []);

    expect(row?.querySelector('th')?.getAttribute('scope')).toBe('row');
    expect(row?.querySelector('th')?.textContent?.trim()).toBe('16.18.1');
    expect(links.map((link) => link.textContent?.trim())).toEqual([
      'Changements',
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
