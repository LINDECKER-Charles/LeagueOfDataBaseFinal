import { Dialog } from '@angular/cdk/dialog';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { ChampionSkin } from '../../../../../core/api/generated/models/champion-skin';
import type { ChampionSkins } from '../../../../../core/api/generated/models/champion-skins';
import { provideTestTransloco } from '../../../../../core/testing/provide-test-transloco';
import { alternateSkins } from './alternate-skins';
import { SkinGallery } from './skin-gallery';
import { SkinShelf } from './skin-shelf';
import { SkinViewer } from './skin-viewer';
import { viewerIndexAfter } from './viewer/viewer-index-after';

const CHAMPIONS = {
  viewer: {
    close: 'Close',
    previous: 'Previous',
    next: 'Next',
    counter: '{{ index }} / {{ count }}',
  },
  chromas: { label: 'Chromas of {{ skin }}' },
};

function skinOf(number: number, name: string, chromas = 0): ChampionSkin {
  const art = (kind: string) => `https://cdn.test/Annie_${number}.${kind}.jpg`;
  return {
    id: `1${String(number).padStart(3, '0')}`,
    number,
    name,
    art: { splash: art('splash'), centered: art('centered'), loading: art('loading') },
    chromas: Array.from({ length: chromas }, (_, index) => ({
      id: number * 100 + index,
      name: `${name} (${String(index)})`,
      label: 'Ruby',
      colors: ['#e0115f', '#9b111e'],
      swatch: `https://cdn.test/chroma_${String(index)}.png`,
    })),
  };
}

const SKINS = [
  skinOf(0, 'default'),
  skinOf(1, 'Goth Annie'),
  skinOf(2, 'Red Riding Annie', 3),
  skinOf(3, 'Annie in Wonderland'),
];

describe('alternateSkins', () => {
  it('leaves out the base skin, which the hero already wears', () => {
    expect(alternateSkins(SKINS).map((skin) => skin.number)).toEqual([1, 2, 3]);
  });
});

describe('viewerIndexAfter', () => {
  const at = { index: 0, count: 3 };

  it('steps with the arrows in the reading direction, wrapping around', () => {
    expect(viewerIndexAfter('ArrowRight', 'ltr', at)).toBe(1);
    expect(viewerIndexAfter('ArrowLeft', 'ltr', at)).toBe(2);
    expect(viewerIndexAfter('ArrowLeft', 'rtl', at)).toBe(1);
  });

  it('reaches either end with Home and End', () => {
    expect(viewerIndexAfter('End', 'ltr', at)).toBe(2);
    expect(viewerIndexAfter('Home', 'ltr', { index: 2, count: 3 })).toBe(0);
  });

  it('ignores other keys, and a viewer with nowhere to go', () => {
    expect(viewerIndexAfter('Enter', 'ltr', at)).toBeNull();
    expect(viewerIndexAfter('ArrowRight', 'ltr', { index: 0, count: 1 })).toBeNull();
  });
});

function translations() {
  return provideTestTransloco({ byPath: { 'champions/en': CHAMPIONS } });
}

@Component({
  imports: [SkinGallery],
  template: '<lodb-skin-gallery [skins]="skins()" championName="Annie" />',
})
class Host {
  readonly skins = signal(SKINS);
}

describe('lodb-skin-gallery', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [translations()] });
  });

  afterEach(() => {
    TestBed.inject(Dialog).closeAll();
  });

  async function render(): Promise<ComponentFixture<Host>> {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    return fixture;
  }

  function tiles(fixture: ComponentFixture<Host>): HTMLButtonElement[] {
    const element = fixture.nativeElement as HTMLElement;
    return [...element.querySelectorAll<HTMLButtonElement>('button.tile')];
  }

  function viewer(): HTMLElement | null {
    return document.querySelector('lodb-skin-viewer');
  }

  async function openTile(fixture: ComponentFixture<Host>, index: number): Promise<HTMLElement> {
    tiles(fixture)[index]?.click();
    await fixture.whenStable();
    return viewer() as HTMLElement;
  }

  // From the focused element, as a reader's key: the dialog's container, not the viewer.
  async function press(fixture: ComponentFixture<Host>, key: string): Promise<void> {
    const focused = document.activeElement;
    focused?.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
    await fixture.whenStable();
  }

  const splash = () => viewer()?.querySelector('img.splash')?.getAttribute('src');
  const counter = () => viewer()?.querySelector('figcaption span')?.textContent?.trim();

  it('shows a tile per alternate skin, its splash loaded lazily from the API', async () => {
    const fixture = await render();

    expect(tiles(fixture).map((tile) => tile.getAttribute('aria-label'))).toEqual([
      'Goth Annie',
      'Red Riding Annie',
      'Annie in Wonderland',
    ]);
    const image = tiles(fixture)[0]?.querySelector('img');
    expect(image?.getAttribute('src')).toBe('https://cdn.test/Annie_1.splash.jpg');
    expect(image?.getAttribute('loading')).toBe('lazy');
  });

  it('badges a skin with its chroma count, and lists its chromas below', async () => {
    const fixture = await render();
    const items = (fixture.nativeElement as HTMLElement).querySelectorAll('li');

    expect(items[0]?.querySelector('.badge')).toBeNull();
    expect(items[1]?.querySelector('.badge')?.textContent?.trim()).toBe('3');
    // The bare count, as the legacy badge: no sentence to agree with a single chroma.
    expect(items[1]?.querySelector('.badge')?.getAttribute('title')).toBe('3');
    expect(items[1]?.querySelectorAll('lodb-chroma-strip .swatch')).toHaveLength(3);
  });

  it('opens the lightbox on the skin picked, named under the champion', async () => {
    const fixture = await render();

    const opened = await openTile(fixture, 1);

    expect(opened.querySelector('figcaption .eyebrow')?.textContent?.trim()).toBe('Annie');
    expect(opened.querySelector('h2')?.textContent?.trim()).toBe('Red Riding Annie');
    expect(opened.querySelector('h2')?.id).toBe(SkinViewer.HEADING_ID);
    expect(splash()).toBe('https://cdn.test/Annie_2.splash.jpg');
    expect(counter()).toBe('2 / 3');
    expect(document.querySelector('.cdk-overlay-pane')?.classList).toContain('hx-lightbox');
  });

  it('closes from the button over the picture', async () => {
    const fixture = await render();
    const opened = await openTile(fixture, 0);

    opened.querySelector<HTMLButtonElement>('button[aria-label="Close"]')?.click();
    await fixture.whenStable();

    expect(viewer()).toBeNull();
  });

  it('starts the strip over at its first tile for another champion', async () => {
    const fixture = await render();
    const strip = (fixture.nativeElement as HTMLElement).querySelector('ul') as HTMLElement;
    strip.scrollLeft = 120;

    fixture.componentInstance.skins.set([SKINS[0] as ChampionSkin, SKINS[3] as ChampionSkin]);
    await fixture.whenStable();

    expect(strip.scrollLeft).toBe(0);
  });

  it('steps through the skins with the arrow buttons, wrapping around', async () => {
    const fixture = await render();
    const opened = await openTile(fixture, 2);

    opened.querySelector<HTMLButtonElement>('button[aria-label="Next"]')?.click();
    await fixture.whenStable();
    expect(splash()).toBe('https://cdn.test/Annie_1.splash.jpg');
    expect(counter()).toBe('1 / 3');

    opened.querySelector<HTMLButtonElement>('button[aria-label="Previous"]')?.click();
    await fixture.whenStable();
    expect(counter()).toBe('3 / 3');
  });

  it('steps with the arrow keys, and reaches the first skin with Home', async () => {
    const fixture = await render();
    await openTile(fixture, 0);
    expect(document.activeElement?.localName).toBe('cdk-dialog-container');

    await press(fixture, 'ArrowLeft');
    expect(counter()).toBe('3 / 3');

    await press(fixture, 'Home');
    expect(splash()).toBe('https://cdn.test/Annie_1.splash.jpg');
  });

  it('shows no arrows nor counter for a single skin', async () => {
    const fixture = await render();
    fixture.componentInstance.skins.set([SKINS[0] as ChampionSkin, SKINS[1] as ChampionSkin]);
    await fixture.whenStable();

    const opened = await openTile(fixture, 0);

    expect(opened.querySelector('lodb-viewer-arrows')).toBeNull();
    expect(counter()).toBeUndefined();
  });
});

describe('lodb-skin-shelf', () => {
  const SKINS_URL = '/api/catalog/16.19.1/en_US/champions/Annie/skins';
  // The delay the shelf waits before its one more try.
  const RETRY_DELAY_MS = 5_000;
  const ANSWER: ChampionSkins = {
    version: '16.19.1',
    language: 'en_US',
    champion: 'Annie',
    skins: SKINS,
  };
  const COLD = { status: 503, statusText: 'Service Unavailable' };

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), translations()],
    });
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    vi.useRealTimers();
  });

  async function settle(fixture: ComponentFixture<SkinShelf>, ms = 0): Promise<void> {
    await vi.advanceTimersByTimeAsync(ms);
    fixture.detectChanges();
  }

  async function shelf(): Promise<ComponentFixture<SkinShelf>> {
    const fixture = TestBed.createComponent(SkinShelf);
    const inputs = { version: '16.19.1', language: 'en_US', champion: 'Annie' };
    for (const [name, value] of Object.entries({ ...inputs, championName: 'Annie' })) {
      fixture.componentRef.setInput(name, value);
    }
    await settle(fixture);
    return fixture;
  }

  function shown(fixture: ComponentFixture<SkinShelf>): { tiles: number; placeholders: number } {
    const element = fixture.nativeElement as HTMLElement;
    return {
      tiles: element.querySelectorAll('button.tile').length,
      placeholders: element.querySelectorAll('lodb-skeleton').length,
    };
  }

  it('holds placeholder tiles while the skins are read, then shows the gallery', async () => {
    const fixture = await shelf();
    expect(shown(fixture)).toEqual({ tiles: 0, placeholders: 3 });

    TestBed.inject(HttpTestingController).expectOne(SKINS_URL).flush(ANSWER);
    await settle(fixture);

    expect(shown(fixture)).toEqual({ tiles: 3, placeholders: 0 });
  });

  it('tries once more after a failure, once the delay a cold catalog asks for passed', async () => {
    const fixture = await shelf();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(SKINS_URL).flush(null, COLD);

    await settle(fixture, RETRY_DELAY_MS - 1);
    http.expectNone(SKINS_URL);
    expect(shown(fixture)).toEqual({ tiles: 0, placeholders: 3 });
    await settle(fixture, 1);
    http.expectOne(SKINS_URL).flush(ANSWER);
    await settle(fixture);

    expect(shown(fixture)).toEqual({ tiles: 3, placeholders: 0 });
  });

  it('leaves the section empty after a second failure, and asks no more', async () => {
    const fixture = await shelf();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(SKINS_URL).flush(null, COLD);
    await settle(fixture, RETRY_DELAY_MS);
    http.expectOne(SKINS_URL).flush(null, COLD);

    await settle(fixture, RETRY_DELAY_MS * 10);

    http.expectNone(SKINS_URL);
    expect(shown(fixture)).toEqual({ tiles: 0, placeholders: 0 });
  });
});
