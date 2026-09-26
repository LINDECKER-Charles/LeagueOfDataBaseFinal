import { Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ChampionDetails } from '../../../../../core/api/generated/models/champion-details';
import type { PageContext } from '../../../../../core/context/page-context';
import { ChampionHero } from './champion-hero';
import { ratingGaugesOf } from './rating-gauges-of';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_US' };

function detailsOf(
  id: string,
  ratings: ChampionDetails['profile']['ratings'] = null,
): ChampionDetails {
  const art = (kind: string) => `https://cdn.test/${id}_0.${kind}.jpg`;
  return {
    canonicalPath: `champions/${id}`,
    version: '16.19.1',
    language: 'en_US',
    profile: {
      canonicalPath: `champions/${id}`,
      id,
      key: '1',
      name: id,
      title: 'the Dark Child',
      image: { status: 'absent' },
      partype: 'Mana',
      resource: 'mana',
      stats: [],
      tags: ['Mage', 'Support'],
      ratings,
    },
    art: { splash: art('splash'), centered: art('centered'), loading: art('loading') },
    blurb: '',
    abilities: [],
    skins: [],
    allyTips: [],
    enemyTips: [],
  };
}

const EN = {
  champion: {
    detail: {
      partype: 'Resource: {{ partype }}',
      profile: { attack: 'Attack', defense: 'Defense', magic: 'Magic', difficulty: 'Difficulty' },
    },
  },
};

const RATINGS = { attack: 2, defense: 3, magic: 10, difficulty: 6 };

describe('ratingGaugesOf', () => {
  it('orders the ratings attack, defense, magic, difficulty', () => {
    expect(ratingGaugesOf(RATINGS)).toEqual([
      { key: 'attack', value: 2 },
      { key: 'defense', value: 3 },
      { key: 'magic', value: 10 },
      { key: 'difficulty', value: 6 },
    ]);
  });

  it('shows no gauge for a champion without ratings', () => {
    expect(ratingGaugesOf(null)).toEqual([]);
    expect(ratingGaugesOf(undefined)).toEqual([]);
  });
});

@Component({
  imports: [ChampionHero],
  template: '<lodb-champion-hero [details]="details()" [context]="context" />',
})
class Host {
  readonly details = signal(detailsOf('Annie', RATINGS));
  readonly context = CONTEXT;
}

describe('lodb-champion-hero', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideTransloco({
          config: {
            availableLangs: ['en'],
            defaultLang: 'en',
            missingHandler: { logMissingKey: false },
            prodMode: true,
          },
          loader: class {
            getTranslation = () => of(EN);
          },
        }),
      ],
    });
  });

  async function render(): Promise<{ fixture: ComponentFixture<Host>; element: HTMLElement }> {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  const texts = (element: HTMLElement, selector: string) =>
    [...element.querySelectorAll(selector)].map((node) => node.textContent?.trim());

  it('titles the page with the champion, its capitalized title, tags and resource', async () => {
    const { element } = await render();

    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Annie');
    expect(element.querySelector('h1 + p')?.textContent).toBe('The Dark Child');
    expect(texts(element, '.hx-chip')).toEqual(['Mage', 'Support', 'Resource: Mana']);
    expect(element.querySelector('.eyebrow')?.textContent).toContain('16.19.1 · en_US');
  });

  it('draws a gauge of ten diamonds per rating, as many lit as the rating', async () => {
    const { element } = await render();
    const gauges = [...element.querySelectorAll('.gauge')];

    expect(gauges.map((gauge) => gauge.querySelectorAll('i.on').length)).toEqual([2, 3, 10, 6]);
    expect(gauges[0]?.querySelectorAll('i')).toHaveLength(10);
    expect(gauges[0]?.classList).toContain('gauge--attack');
    expect(gauges[2]?.getAttribute('aria-label')).toBe('Magic : 10/10');
  });

  it('draws no gauge for a champion without ratings', async () => {
    const { fixture, element } = await render();

    fixture.componentInstance.details.set(detailsOf('Brand'));
    await fixture.whenStable();

    expect(element.querySelector('.gauge')).toBeNull();
  });

  it('shows the centred art, and the full splash when it fails, until the next champion', async () => {
    const { fixture, element } = await render();
    const splash = () => element.querySelector('section > img')?.getAttribute('src');
    expect(splash()).toBe('https://cdn.test/Annie_0.centered.jpg');

    element.querySelector('section > img')?.dispatchEvent(new Event('error'));
    await fixture.whenStable();
    expect(splash()).toBe('https://cdn.test/Annie_0.splash.jpg');

    fixture.componentInstance.details.set(detailsOf('Brand', RATINGS));
    await fixture.whenStable();
    expect(splash()).toBe('https://cdn.test/Brand_0.centered.jpg');
  });
});
