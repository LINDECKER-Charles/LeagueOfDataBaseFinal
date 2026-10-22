import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { DeferBlockState, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import type { ChampionCard } from '../../../../core/api/generated/models/champion-card';
import type { ChampionDetails } from '../../../../core/api/generated/models/champion-details';
import type { ChampionSkins } from '../../../../core/api/generated/models/champion-skins';
import type { DetailNeighbour } from '../../../../core/api/generated/models/detail-neighbour';
import type { PageContext } from '../../../../core/context/page-context';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { Seo } from '../../../../core/seo/seo';
import type { SeoPage } from '../../../../core/seo/seo-page';
import { provideTestTransloco } from '../../../../core/testing/provide-test-transloco';
import { ChampionPage } from './champion-page';
import { detailStateOf } from '../../../../core/testing/detail-state-of';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };
const ART = { splash: 'splash.jpg', centered: 'centered.jpg', loading: 'loading.jpg' };
const SKINS = '/api/catalog/16.19.1/en_US/champions/Annie/skins';

function cardOf(id: string): ChampionCard {
  return {
    canonicalPath: `champions/${id}`,
    id,
    key: id,
    name: id,
    title: 'the Dark Child',
    image: { status: 'absent' },
    loadingArt: `https://ddragon.leagueoflegends.com/cdn/img/champion/loading/${id}_0.jpg`,
    loadingArtVariants: [],
    blurb: 'Dangerous, yet disarmingly precocious.',
    partype: 'Mana',
    resource: 'mana',
    stats: [{ stat: 'health', base: 560, perLevel: 96 }],
    tags: ['Mage'],
  };
}

function neighbourOf(id: string): DetailNeighbour {
  return { id, name: id, canonicalPath: `champions/${id}`, edition: 'modern' };
}

function detailsOf(overrides: Partial<ChampionDetails> = {}): ChampionDetails {
  return {
    canonicalPath: 'champions/Annie',
    ...detailStateOf('champions/Annie'),
    version: '16.19.1',
    language: 'en_US',
    profile: cardOf('Annie'),
    art: ART,
    centeredArtVariants: [],
    blurb: 'Dangerous, yet disarmingly precocious.',
    lore: 'Annie is a <i>child</i> mage<script>alert(1)</script>.',
    abilities: [
      { slot: 'q', name: 'Disintegrate', description: 'Burns.', image: { status: 'absent' } },
    ],
    alternateSkinCount: 1,
    alternateSkinNames: ['Goth Annie'],
    allyTips: ['Stun with the passive.', ' '],
    enemyTips: [],
    neighbours: { previous: neighbourOf('Ahri'), next: neighbourOf('Brand') },
    ...overrides,
  };
}

const ANNIE_SKINS: ChampionSkins = {
  version: '16.19.1',
  language: 'en_US',
  champion: 'Annie',
  skins: [
    { id: '1000', number: 0, name: 'Annie', art: ART, chromas: [] },
    { id: '1001', number: 1, name: 'Goth Annie', art: ART, chromas: [] },
  ],
};

const EN = {
  champion: {
    detail: {
      abilities: 'Abilities',
      skins: 'Skins',
      lore: { title: 'Lore' },
      tips: 'Tips',
      stats: { title: 'Base Statistics' },
    },
  },
};

@Component({ template: '' })
class Blank {}

async function visit(details: ChampionDetails, url = '/en/champions/Annie') {
  const apply = vi.fn<(page: SeoPage) => Promise<void>>().mockResolvedValue(undefined);
  const entry: CatalogueEntry<ChampionDetails> = { context: CONTEXT, details };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([
        { path: 'en/champions/:id', resolve: { entry: () => entry }, component: ChampionPage },
        { path: '**', component: Blank },
      ]),
      provideTestTransloco({ byPath: { en: EN } }),
      { provide: Seo, useValue: { apply } },
    ],
  });
  const harness = await RouterTestingHarness.create(url);
  await harness.fixture.whenStable();
  return { harness, apply, host: harness.routeNativeElement as HTMLElement };
}

describe('ChampionPage', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'IntersectionObserver',
      class {
        observe = vi.fn();
        unobserve = vi.fn();
        disconnect = vi.fn();
      },
    );
    Object.defineProperty(document.defaultView, 'matchMedia', {
      configurable: true,
      value: () => ({ matches: true }),
    });
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    TestBed.resetTestingModule();
    Reflect.deleteProperty(document.defaultView ?? {}, 'matchMedia');
    vi.unstubAllGlobals();
  });

  it('lays out the champion, section by section, with the stats aside', async () => {
    const { host } = await visit(detailsOf());
    const sections = [...host.querySelectorAll('section[id], aside[id]')].map((node) => node.id);

    expect(host.querySelector('h1')?.textContent?.trim()).toBe('Annie');
    expect(sections).toEqual(['abilities', 'skins', 'lore', 'tips', 'stats']);
    expect(host.querySelectorAll('#tips li')).toHaveLength(1);
    expect(host.querySelector('lodb-stat-board')).not.toBeNull();
  });

  it('labels its section chips from the root catalogue, like the legacy tabs', async () => {
    const { host } = await visit(detailsOf());
    const chips = [...host.querySelectorAll('.section-nav a')].map((a) => a.textContent?.trim());

    expect(chips).toEqual(['Abilities', 'Skins', 'Lore', 'Tips', 'Base Statistics']);
  });

  it('keeps the lore markup Data Dragon uses, and makes anything else plain text', async () => {
    const { host } = await visit(detailsOf());
    const lore = host.querySelector('.ddragon-rich--lore');

    expect(lore?.querySelector('i')?.textContent).toBe('child');
    expect(lore?.querySelector('script')).toBeNull();
    expect(lore?.textContent).toBe('Annie is a child magealert(1).');
  });

  it('leaves out the sections a champion has nothing for, the blurb standing for its lore', async () => {
    const { host } = await visit(detailsOf({ lore: null, alternateSkinCount: 0, allyTips: [' '] }));
    const sections = [...host.querySelectorAll('section[id], aside[id]')].map((node) => node.id);

    expect(sections).toEqual(['abilities', 'lore', 'stats']);
    expect(host.querySelector('#lore')?.textContent).toContain('disarmingly precocious');
  });

  it('names the skins until their section is scrolled to, then shows the gallery', async () => {
    const { harness, host } = await visit(detailsOf());
    const http = TestBed.inject(HttpTestingController);
    expect(host.querySelector('#skins .codex-header__meta')?.textContent?.trim()).toBe('1');
    expect(host.querySelectorAll('#skins lodb-skeleton')).toHaveLength(1);
    expect(host.querySelector('#skins li')?.textContent?.trim()).toBe('Goth Annie');
    http.expectNone(SKINS);

    // In the template's order: the abilities, the skins, the stats, the pager.
    const [, gallery] = await harness.fixture.getDeferBlocks();
    await gallery?.render(DeferBlockState.Complete);
    expect(host.querySelectorAll('#skins lodb-skeleton')).toHaveLength(3);
    http.expectOne(SKINS).flush(ANNIE_SKINS);
    await harness.fixture.whenStable();

    expect(host.querySelectorAll('#skins lodb-skeleton')).toHaveLength(0);
    expect(host.querySelectorAll('#skins button.tile')).toHaveLength(1);
  });

  it('pages to the champions on either side, and back to the list', async () => {
    const { host } = await visit(detailsOf());
    const hrefs = [...host.querySelectorAll('lodb-pager a')].map((a) => a.getAttribute('href'));

    expect(hrefs).toEqual(
      expect.arrayContaining(['/en/champions/Ahri', '/en/champions', '/en/champions/Brand']),
    );
  });

  it('keeps the regional variant in the pager, query unescaped', async () => {
    const { host } = await visit(detailsOf(), '/en/champions/Annie?lang=en_GB');
    const hrefs = [...host.querySelectorAll('lodb-pager a')].map((a) => a.getAttribute('href'));

    expect(hrefs).toEqual([
      '/en/champions/Ahri?lang=en_GB',
      '/en/champions?lang=en_GB',
      '/en/champions/Brand?lang=en_GB',
    ]);
  });

  it('writes the head of the champion', async () => {
    const { apply } = await visit(detailsOf());

    expect(apply).toHaveBeenCalledWith(
      expect.objectContaining({ path: 'champions/Annie', version: null }),
    );
  });
});
