import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { SummonerCard } from '../../../../core/api/generated/models/summoner-card';
import type { PageContext } from '../../../../core/context/page-context';
import type { SeoPage } from '../../../../core/seo/seo-page';
import { CatalogueHead } from '../../shared/codex/head/catalogue-head';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { CatalogueLists } from '../../shared/data/catalogue-lists';
import { cooldownTextOf } from './cooldown-text-of';
import { SUMMONER_CARD_ADAPTER } from './summoner-card-adapter';
import { SummonerList } from './summoner-list';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const FLASH: SummonerCard = {
  canonicalPath: 'summoners/SummonerFlash',
  cooldown: [300],
  description: 'Teleports your champion a <b>short distance</b>.',
  edition: 'modern',
  id: 'SummonerFlash',
  image: { status: 'present', url: '/cdn/blobs/flash.png' },
  key: '4',
  modes: [
    { code: 'CLASSIC', facetable: true, label: "Summoner's Rift" },
    { code: 'ARAM', facetable: true, label: 'ARAM' },
    { code: 'TUTORIAL', facetable: false, label: "Summoner's Rift" },
  ],
  name: 'Flash',
  summonerLevel: 7,
};
const JADE: SummonerCard = {
  ...FLASH,
  canonicalPath: 'summoners/SummonerFlash_Jade',
  edition: 'classic',
  id: 'SummonerFlash_Jade',
  modes: [{ code: 'JADE', facetable: false, label: null }],
  summonerLevel: null,
};
const TEXTS: CatalogueTexts = {
  seo: (key, params) => `${key} ${JSON.stringify(params ?? {})}`,
  main: (key) => key,
};

describe('cooldownTextOf', () => {
  it("spells a cooldown as Data Dragon's cooldownBurn does", () => {
    expect(cooldownTextOf([300])).toBe('300');
    expect(cooldownTextOf([210, 210, 210])).toBe('210');
    expect(cooldownTextOf([210, 180])).toBe('210/180');
    expect(cooldownTextOf([])).toBe('–');
  });
});

describe('SUMMONER_CARD_ADAPTER', () => {
  it('filters on the modes the facet offers, the edition, the level and the cooldown', () => {
    expect(SUMMONER_CARD_ADAPTER.valuesOf(FLASH)).toEqual({
      mode: ['CLASSIC', 'ARAM'],
      edition: ['modern'],
      level: ['7'],
      cooldown: 300,
    });
    expect(SUMMONER_CARD_ADAPTER.valuesOf(JADE)).toEqual({
      mode: [],
      edition: ['classic'],
      cooldown: 300,
    });
  });
});

describe('lodb-summoner-list', () => {
  async function render() {
    const heads: SeoPage[] = [];
    const write = (_: string, build: (texts: CatalogueTexts) => SeoPage) =>
      heads.push(build(TEXTS));
    const list = {
      entries: [FLASH, JADE],
      total: 2,
      facets: { levels: [7], modes: ['CLASSIC', 'ARAM'] },
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'en/summoners', component: SummonerList, data: { context: CONTEXT } },
        ]),
        provideTransloco({
          config: {
            availableLangs: ['en'],
            defaultLang: 'en',
            missingHandler: { logMissingKey: false },
            prodMode: true,
          },
          loader: class {
            getTranslation = () => of({ edition: { classic: 'LoL Classic' } });
          },
        }),
        { provide: CatalogueHead, useValue: { write } },
        {
          provide: CatalogueLists,
          useValue: { fetch: () => of({ kind: 'list', list, retryAfterMs: null }) },
        },
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/en/summoners');
    await harness.fixture.whenStable();
    return { element: harness.routeNativeElement as HTMLElement, heads };
  }

  it('lists every spell, the LoL Classic twin marked, each linking its page', async () => {
    const { element } = await render();

    const cards = [...element.querySelectorAll('lodb-entity-card')];
    expect(cards.map((card) => card.querySelector('a')?.getAttribute('href'))).toEqual([
      '/en/summoners/SummonerFlash',
      '/en/summoners/SummonerFlash_Jade',
    ]);
    expect(cards[1]?.querySelector('lodb-edition-badge')?.textContent).toContain('LoL Classic');
  });

  it('names the modes of each card from the whitelist, once each', async () => {
    const { element } = await render();

    const chips = (index: number) =>
      [...element.querySelectorAll('lodb-entity-card')][index]?.querySelectorAll('.hx-chip');
    expect([...(chips(0) ?? [])].map((chip) => chip.textContent?.trim())).toEqual([
      "Summoner's Rift",
      'ARAM',
    ]);
    expect([...(chips(1) ?? [])].map((chip) => chip.textContent?.trim())).toEqual(['LoL Classic']);
  });

  it('writes the head of the list with its count', async () => {
    const { heads } = await render();

    expect(heads.at(-1)?.description).toBe(
      'summoner.list.description {"count":2,"version":"16.19.1"}',
    );
  });
});
