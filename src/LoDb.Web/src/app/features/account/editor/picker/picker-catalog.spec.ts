import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PickerCatalog } from './picker-catalog';

const PRESENT = { status: 'present', url: '/cdn/blobs/flash.png' } as const;
const SUMMONERS = '/api/pickers/summoners?version=16.14.1&lang=fr_FR';

function setUp() {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), PickerCatalog],
  });
  const catalog = TestBed.inject(PickerCatalog);
  catalog.use('16.14.1', 'fr_FR');
  return { catalog, http: TestBed.inject(HttpTestingController) };
}

describe('PickerCatalog', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('reads a list on the version and language of the favorites, once', async () => {
    const { catalog, http } = setUp();

    const first = catalog.favorites('summoner');
    const again = catalog.favorites('summoner');
    http.expectOne(SUMMONERS).flush({
      language: 'fr_FR',
      version: '16.14.1',
      options: [{ id: 'SummonerFlash', key: '4', name: 'Saut éclair', image: PRESENT }],
    });

    expect(await first).toEqual([
      {
        id: 'SummonerFlash',
        name: 'Saut éclair',
        image: '/cdn/blobs/flash.png',
        searchText: 'saut eclair summonerflash',
      },
    ]);
    expect(await again).toBe(await first);
    expect(await catalog.favorites('summoner')).toBe(await first);
  });

  it('asks again after a failure', async () => {
    const { catalog, http } = setUp();

    const failed = catalog.favorites('summoner');
    http.expectOne(SUMMONERS).flush(null, { status: 503, statusText: 'Unavailable' });
    await expect(failed).rejects.toBeDefined();

    const retried = catalog.favorites('summoner');
    http.expectOne(SUMMONERS).flush({ language: 'fr_FR', version: '16.14.1', options: [] });
    expect(await retried).toEqual([]);
  });

  it('reads the skins of a champion as banner tiles', async () => {
    const { catalog, http } = setUp();

    const skins = catalog.skins('Ahri');
    http.expectOne('/api/pickers/skins?version=16.14.1&lang=fr_FR&champion=Ahri').flush({
      skins: [{ id: 'Ahri_7', name: 'Ahri arcade', number: 7, image: '/l.jpg', banner: '/c.jpg' }],
    });

    expect(await skins).toEqual([
      {
        id: 'Ahri_7',
        name: 'Ahri arcade',
        image: '/l.jpg',
        banner: '/c.jpg',
        searchText: 'ahri arcade',
      },
    ]);
  });

  it('forgets the lists of another version', async () => {
    const { catalog, http } = setUp();
    void catalog.favorites('summoner');
    http.expectOne(SUMMONERS).flush({ language: 'fr_FR', version: '16.14.1', options: [] });

    catalog.use('16.13.1', 'fr_FR');
    void catalog.favorites('summoner');

    http
      .expectOne('/api/pickers/summoners?version=16.13.1&lang=fr_FR')
      .flush({ language: 'fr_FR', version: '16.13.1', options: [] });
  });
});
