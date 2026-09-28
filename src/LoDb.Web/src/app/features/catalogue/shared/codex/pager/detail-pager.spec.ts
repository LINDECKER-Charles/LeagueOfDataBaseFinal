import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, type UrlTree, provideRouter } from '@angular/router';
import { TranslocoService, provideTransloco } from '@jsverse/transloco';
import { firstValueFrom, of } from 'rxjs';
import type { DetailNeighbour } from '../../../../../core/api/generated/models/detail-neighbour';
import type { PageContext } from '../../../../../core/context/page-context';
import { injectDetailPager } from './inject-detail-pager';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const TEXTS = { edition: { classic: 'LoL Classic', classic_hint: 'League of Legends Classic' } };
const EXHAUST: DetailNeighbour = {
  id: 'SummonerExhaust_Jade',
  name: 'Exhaust',
  canonicalPath: 'summoners/SummonerExhaust_Jade',
  edition: 'classic',
};
const HEAL: DetailNeighbour = {
  id: 'SummonerHeal',
  name: 'Heal',
  canonicalPath: 'summoners/SummonerHeal',
  edition: 'modern',
};

// On the server, as the pager must render there: nothing below waits for the browser.
async function pagerOn(url: string) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: PLATFORM_ID, useValue: 'server' },
      provideTransloco({
        config: {
          availableLangs: ['en'],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = () => of(TEXTS);
        },
      }),
    ],
  });
  const router = TestBed.inject(Router);
  vi.spyOn(router, 'url', 'get').mockReturnValue(url);
  const pager = TestBed.runInInjectionContext(() =>
    injectDetailPager(() => ({
      resource: 'summoners',
      context: CONTEXT,
      neighbours: { previous: EXHAUST, next: HEAL },
    })),
  );
  await firstValueFrom(TestBed.inject(TranslocoService).load('en'));
  const href = (url: string | UrlTree | undefined) =>
    typeof url === 'string' || url === undefined ? url : router.serializeUrl(url);
  return { pager, href };
}

describe('injectDetailPager', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("links the payload's neighbours and the list", async () => {
    const { pager, href } = await pagerOn('/en/summoners/SummonerFlash');
    const { previous, next, hub } = pager();

    expect([previous?.name, next?.name]).toEqual(['Exhaust', 'Heal']);
    expect(href(previous?.url)).toBe('/en/summoners/SummonerExhaust_Jade');
    expect(href(next?.url)).toBe('/en/summoners/SummonerHeal');
    expect(href(hub)).toBe('/en/summoners');
  });

  it('marks a LoL Classic neighbour only, its name alone being a current one', async () => {
    const { pager } = await pagerOn('/en/summoners/SummonerFlash');

    expect(pager().previous?.mark).toEqual({
      label: 'LoL Classic',
      hint: 'League of Legends Classic',
    });
    expect(pager().next?.mark).toBeUndefined();
  });

  it('keeps the regional variant of the page in every link', async () => {
    const { pager, href } = await pagerOn('/en/summoners/SummonerFlash?lang=en_GB');
    const { previous, hub } = pager();

    expect(href(previous?.url)).toBe('/en/summoners/SummonerExhaust_Jade?lang=en_GB');
    expect(href(hub)).toBe('/en/summoners?lang=en_GB');
  });
});
