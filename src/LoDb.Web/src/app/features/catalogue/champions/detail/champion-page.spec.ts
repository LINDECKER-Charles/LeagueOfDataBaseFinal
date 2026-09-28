import { Component } from '@angular/core';
import { DeferBlockState, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ChampionCard } from '../../../../core/api/generated/models/champion-card';
import type { ChampionDetails } from '../../../../core/api/generated/models/champion-details';
import type { DetailNeighbour } from '../../../../core/api/generated/models/detail-neighbour';
import type { PageContext } from '../../../../core/context/page-context';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { Seo } from '../../../../core/seo/seo';
import type { SeoPage } from '../../../../core/seo/seo-page';
import { ChampionPage } from './champion-page';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };

function cardOf(id: string): ChampionCard {
  return {
    canonicalPath: `champions/${id}`,
    id,
    key: id,
    name: id,
    title: 'the Dark Child',
    image: { status: 'absent' },
    loadingArt: `https://ddragon.leagueoflegends.com/cdn/img/champion/loading/${id}_0.jpg`,
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
  const art = { splash: 'splash.jpg', centered: 'centered.jpg', loading: 'loading.jpg' };
  return {
    canonicalPath: 'champions/Annie',
    version: '16.19.1',
    language: 'en_US',
    profile: cardOf('Annie'),
    art,
    blurb: 'Dangerous, yet disarmingly precocious.',
    lore: 'Annie is a <i>child</i> mage<script>alert(1)</script>.',
    abilities: [
      { slot: 'q', name: 'Disintegrate', description: 'Burns.', image: { status: 'absent' } },
    ],
    skins: [
      { id: '1000', number: 0, name: 'default', art, chromas: [] },
      { id: '1001', number: 1, name: 'Goth Annie', art, chromas: [] },
    ],
    allyTips: ['Stun with the passive.', ' '],
    enemyTips: [],
    neighbours: { previous: neighbourOf('Ahri'), next: neighbourOf('Brand') },
    ...overrides,
  };
}

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
      provideRouter([
        { path: 'en/champions/:id', resolve: { entry: () => entry }, component: ChampionPage },
        { path: '**', component: Blank },
      ]),
      provideTransloco({
        config: {
          availableLangs: ['en'],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = (path: string) => of(path === 'en' ? EN : {});
        },
      }),
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
    const { host } = await visit(detailsOf({ lore: null, skins: [], allyTips: [' '] }));
    const sections = [...host.querySelectorAll('section[id], aside[id]')].map((node) => node.id);

    expect(sections).toEqual(['abilities', 'lore', 'stats']);
    expect(host.querySelector('#lore')?.textContent).toContain('disarmingly precocious');
  });

  it('loads the skin gallery once its section is scrolled to', async () => {
    const { harness, host } = await visit(detailsOf());
    expect(host.querySelector('lodb-skin-gallery')).toBeNull();
    expect(host.querySelectorAll('#skins lodb-skeleton')).toHaveLength(3);

    const [gallery] = await harness.fixture.getDeferBlocks();
    await gallery?.render(DeferBlockState.Complete);

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
