import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, throwError } from 'rxjs';
import type { CatalogMeta } from '../../../../core/api/generated/models/catalog-meta';
import { CatalogService } from '../../../../core/api/generated/services/catalog.service';
import { ApiMeta } from '../../../../core/api/meta/api-meta';
import { Inventory } from './inventory';
import { inventoryTiles } from './inventory-tiles';

const META = {
  defaultLanguage: 'en_US',
  latest: '15.14.1',
  languages: ['en_US', 'fr_FR', 'de_DE'],
  versions: ['15.14.1', '15.13.1'],
  locales: [{ locale: 'fr', language: 'fr_FR' }],
} as unknown as CatalogMeta;

function page(total: number) {
  return of({ total });
}

function inventoryWith(meta: CatalogMeta | Error, catalog: Partial<Record<string, unknown>>) {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: ApiMeta,
        useValue: { meta: () => (meta instanceof Error ? throwError(() => meta) : of(meta)) },
      },
      { provide: CatalogService, useValue: catalog },
    ],
  });
  return TestBed.inject(Inventory);
}

function catalogOf(listChampions = vi.fn(() => page(172))) {
  return {
    listChampions,
    listItems: vi.fn(() => page(311)),
    // 63 runes over 5 paths: the inventory counts the paths.
    listRunes: vi.fn(() => of({ total: 63, trees: [{}, {}, {}, {}, {}] })),
    listSummoners: vi.fn(() => page(18)),
  };
}

describe('Inventory', () => {
  it("counts each list on the latest version, in the locale's language", async () => {
    const catalog = catalogOf();

    const snapshot = await firstValueFrom(inventoryWith(META, catalog).snapshot('fr'));

    expect(snapshot).toEqual({
      version: '15.14.1',
      champions: 172,
      items: 311,
      runes: 5,
      summoners: 18,
      languages: 3,
      versions: 2,
    });
    expect(catalog.listChampions).toHaveBeenCalledWith({
      version: '15.14.1',
      lang: 'fr_FR',
      page: 1,
      size: 1,
    });
  });

  it('leaves a count unknown when its list fails, and keeps the others', async () => {
    const catalog = catalogOf(vi.fn(() => throwError(() => new Error('503'))));

    const snapshot = await firstValueFrom(inventoryWith(META, catalog).snapshot('de'));

    expect(snapshot.champions).toBeNull();
    expect(snapshot.items).toBe(311);
    expect(catalog.listItems).toHaveBeenCalledWith(expect.objectContaining({ lang: 'en_US' }));
  });

  it('asks no list before the first ingestion', async () => {
    const catalog = catalogOf();

    const snapshot = await firstValueFrom(
      inventoryWith({ ...META, latest: null }, catalog).snapshot('en'),
    );

    expect(snapshot).toMatchObject({ version: null, champions: null, languages: 3, versions: 2 });
    expect(catalog.listChampions).not.toHaveBeenCalled();
  });

  it('knows nothing when the API does not answer', async () => {
    const snapshot = await firstValueFrom(
      inventoryWith(new Error('offline'), catalogOf()).snapshot('en'),
    );

    expect(Object.values(snapshot).every((value) => value === null)).toBe(true);
  });
});

describe('inventoryTiles', () => {
  it('shows a dash for every fact before the snapshot arrives', () => {
    expect(inventoryTiles(['champions', 'version'], null)).toEqual([
      { label: 'header.navigation.champion', value: '—' },
      { label: 'about.data.snapshot.patch', value: '—' },
    ]);
  });

  it('shows the facts asked for, in their order, a dash for an unknown one', () => {
    const snapshot = {
      version: '15.14.1',
      champions: 172,
      items: null,
      runes: 63,
      summoners: 0,
      languages: 3,
      versions: 2,
    };

    expect(inventoryTiles(['summoners', 'items', 'version'], snapshot)).toEqual([
      { label: 'header.navigation.summoner', value: '0' },
      { label: 'header.navigation.item', value: '—' },
      { label: 'about.data.snapshot.patch', value: '15.14.1' },
    ]);
  });
});
