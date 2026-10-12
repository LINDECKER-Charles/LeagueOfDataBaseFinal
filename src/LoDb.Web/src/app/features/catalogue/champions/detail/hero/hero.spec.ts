import { Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { ChampionDetails } from '../../../../../core/api/generated/models/champion-details';
import type { PageContext } from '../../../../../core/context/page-context';
import { provideTestTransloco } from '../../../../../core/testing/provide-test-transloco';
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
      loadingArt: art('loading'),
      loadingArtVariants: [],
      blurb: '',
      partype: 'Mana',
      resource: 'mana',
      stats: [],
      tags: ['Mage', 'Support'],
      ratings,
    },
    art: { splash: art('splash'), centered: art('centered'), loading: art('loading') },
    centeredArtVariants: [],
    blurb: '',
    abilities: [],
    alternateSkinCount: 0,
    allyTips: [],
    enemyTips: [],
    neighbours: { previous: null, next: null },
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
      providers: [provideTestTransloco({ otherwise: EN })],
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
    const splash = () => element.querySelector('section picture > img')?.getAttribute('src');
    expect(splash()).toBe('https://cdn.test/Annie_0.centered.jpg');
    expect(element.querySelector('section picture > source')).toBeNull();

    element.querySelector('section picture > img')?.dispatchEvent(new Event('error'));
    await fixture.whenStable();
    expect(splash()).toBe('https://cdn.test/Annie_0.splash.jpg');

    fixture.componentInstance.details.set(detailsOf('Brand', RATINGS));
    await fixture.whenStable();
    expect(splash()).toBe('https://cdn.test/Brand_0.centered.jpg');
  });

  it('fetches the stored variants first, at high priority, at the drawn width', async () => {
    const { fixture, element } = await render();
    fixture.componentInstance.details.set({
      ...detailsOf('Annie', RATINGS),
      centeredArtVariants: [
        { url: '/cdn/blobs/a.webp', width: 480 },
        { url: '/cdn/blobs/b.webp', width: 960 },
      ],
    });
    await fixture.whenStable();
    const source = () => element.querySelector('section picture > source');
    const img = () => element.querySelector('section picture > img');

    expect(source()?.getAttribute('type')).toBe('image/webp');
    expect(source()?.getAttribute('srcset')).toBe('/cdn/blobs/a.webp 480w, /cdn/blobs/b.webp 960w');
    expect(source()?.getAttribute('sizes')).toContain('100vw');
    expect(img()?.getAttribute('fetchpriority')).toBe('high');
    expect(img()?.getAttribute('loading')).toBe('eager');

    img()?.dispatchEvent(new Event('error'));
    await fixture.whenStable();
    expect(source()).toBeNull();
    expect(img()?.getAttribute('src')).toBe('https://cdn.test/Annie_0.centered.jpg');

    img()?.dispatchEvent(new Event('error'));
    await fixture.whenStable();
    expect(img()?.getAttribute('src')).toBe('https://cdn.test/Annie_0.splash.jpg');
  });

  it('steps down once for variants that failed before hydration, to the centred art', async () => {
    // What hydration finds of an image that failed unheard: complete, without a width. The
    // browser only reloads the image once the render is over, so the DOM still says so then.
    vi.spyOn(HTMLImageElement.prototype, 'complete', 'get').mockReturnValue(true);
    vi.spyOn(HTMLImageElement.prototype, 'naturalWidth', 'get').mockReturnValue(0);
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.details.set({
      ...detailsOf('Annie', RATINGS),
      centeredArtVariants: [{ url: '/cdn/blobs/a.webp', width: 480 }],
    });
    await fixture.whenStable();
    vi.restoreAllMocks();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('section picture > source')).toBeNull();
    expect(element.querySelector('section picture > img')?.getAttribute('src')).toBe(
      'https://cdn.test/Annie_0.centered.jpg',
    );
  });
});
