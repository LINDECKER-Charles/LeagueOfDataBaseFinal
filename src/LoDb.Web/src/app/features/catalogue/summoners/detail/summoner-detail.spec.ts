import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { CatalogImage } from '../../../../core/api/generated/models/catalog-image';
import type { SummonerCard } from '../../../../core/api/generated/models/summoner-card';
import type { SummonerDetails } from '../../../../core/api/generated/models/summoner-details';
import type { PageContext } from '../../../../core/context/page-context';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import type { SeoPage } from '../../../../core/seo/seo-page';
import { CatalogueHead } from '../../shared/codex/head/catalogue-head';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { SummonerDetail } from './summoner-detail';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const CARD: SummonerCard = {
  canonicalPath: 'summoners/SummonerFlash_Jade',
  cooldown: [300],
  counterpart: {
    id: 'SummonerFlash',
    name: 'Flash',
    edition: 'modern',
    canonicalPath: 'summoners/SummonerFlash',
  },
  description: 'Teleports your champion a <b>short distance</b>.',
  edition: 'classic',
  id: 'SummonerFlash_Jade',
  image: { status: 'present', url: '/cdn/blobs/flash.png' },
  key: '4',
  modes: [
    { code: 'JADE', facetable: false, label: null },
    { code: 'WIPMODEWIP', facetable: false },
  ],
  name: 'Flash',
  summonerLevel: 7,
};
const DETAILS: SummonerDetails = {
  canonicalPath: CARD.canonicalPath,
  language: 'en_US',
  version: CONTEXT.version,
  cost: [0],
  globalRange: false,
  range: [425],
  profile: CARD,
  neighbours: {
    previous: {
      id: 'SummonerExhaust_Jade',
      name: 'Exhaust',
      canonicalPath: 'summoners/SummonerExhaust_Jade',
      edition: 'classic',
    },
    next: {
      id: 'SummonerHeal',
      name: 'Heal',
      canonicalPath: 'summoners/SummonerHeal',
      edition: 'modern',
    },
  },
};
const ENTRY: CatalogueEntry<SummonerDetails> = { context: CONTEXT, details: DETAILS };
const TEXTS: CatalogueTexts = {
  seo: (key, params) => `${key} ${JSON.stringify(params ?? {})}`,
  main: (key) => (key === 'edition.classic' ? 'LoL Classic' : key),
};

async function render(entry = ENTRY) {
  const heads: SeoPage[] = [];
  const write = (_: string, build: (texts: CatalogueTexts) => SeoPage) => heads.push(build(TEXTS));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'en/summoners/:id', component: SummonerDetail, data: { entry } }]),
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
    ],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/en/summoners/SummonerFlash_Jade');
  await harness.fixture.whenStable();
  return { element: harness.routeNativeElement as HTMLElement, heads };
}

describe('lodb-summoner-detail', () => {
  beforeEach(() => {
    // Reduced motion: lodbReveal shows the sections at once, without an observer.
    vi.stubGlobal('matchMedia', () => ({ matches: true }));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('seals the spell with its cooldown, and links a LoL Classic twin to the current spell', async () => {
    const { element } = await render();

    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Flash');
    expect(element.querySelector('.seal lodb-catalogue-image')).not.toBeNull();
    expect(element.querySelector('header')?.textContent).toContain('300s');
    const twin = element.querySelector<HTMLAnchorElement>('lodb-edition-counterpart a');
    expect(twin?.getAttribute('href')).toBe('/en/summoners/SummonerFlash');
    expect(twin?.dataset['edition']).toBe('modern');
  });

  it('marks a spell without art by its initials in the seal, as the legacy did', async () => {
    const absent: CatalogImage = { status: 'absent' };
    const profile = { ...CARD, image: absent };
    const { element } = await render({ context: CONTEXT, details: { ...DETAILS, profile } });

    expect(element.querySelector('.seal lodb-catalogue-image')).toBeNull();
    expect(element.querySelector('.seal')?.textContent?.trim()).toBe('FL');
  });

  it('names its modes from the whitelist, LoL Classic by its edition', async () => {
    const { element } = await render();

    const modes = [...element.querySelectorAll('[data-testid="modes"] li')];
    expect(modes.map((mode) => mode.textContent?.trim())).toEqual(['LoL Classic']);
  });

  it('engraves its plaques, and turns the pages, a LoL Classic neighbour marked', async () => {
    const { element } = await render();

    expect(element.querySelectorAll('.hx-plate')).toHaveLength(2);
    expect(element.querySelector('lodb-pager a[rel="next"]')?.getAttribute('href')).toBe(
      '/en/summoners/SummonerHeal',
    );
    const previous = element.querySelector('lodb-pager a[rel="prev"] .pager__name');
    expect(previous?.textContent?.trim()).toBe('Exhaust LoL Classic');
    expect(element.querySelector('lodb-pager a[rel="next"] .hx-chip-hex')).toBeNull();
  });

  it('names the LoL Classic edition in its head', async () => {
    const { heads } = await render();

    expect(heads.at(-1)?.title).toBe('summoner.detail.title {"name":"Flash (LoL Classic)"}');
    expect(heads.at(-1)?.image).toBe('/preview/summoners.png');
  });
});
