import { Dialog } from '@angular/cdk/dialog';
import { Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ChampionSkin } from '../../../../../core/api/generated/models/champion-skin';
import { alternateSkins } from './alternate-skins';
import { SkinGallery } from './skin-gallery';
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

@Component({
  imports: [SkinGallery],
  template: '<lodb-skin-gallery [skins]="skins()" championName="Annie" />',
})
class Host {
  readonly skins = signal(SKINS);
}

describe('lodb-skin-gallery', () => {
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
            getTranslation = (path: string) => of(path.startsWith('champions/') ? CHAMPIONS : {});
          },
        }),
      ],
    });
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
    expect(items[1]?.querySelectorAll('lodb-chroma-strip .swatch')).toHaveLength(3);
  });

  it('opens the viewer on the skin picked, named after the champion', async () => {
    const fixture = await render();

    const opened = await openTile(fixture, 1);

    expect(opened.querySelector('h2')?.textContent?.trim()).toBe('Annie');
    expect(splash()).toBe('https://cdn.test/Annie_2.splash.jpg');
    expect(opened.querySelector('figcaption p')?.textContent?.trim()).toBe('Red Riding Annie');
    expect(counter()).toBe('2 / 3');
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
