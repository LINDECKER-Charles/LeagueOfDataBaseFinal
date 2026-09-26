import { Dialog } from '@angular/cdk/dialog';
import { Component } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { ChampionChroma } from '../../../../../../core/api/generated/models/champion-chroma';
import { ChromaStrip } from './chroma-strip';
import { chromaRamp } from './chroma-ramp';

const CHAMPIONS = {
  viewer: {
    close: 'Close',
    previous: 'Previous',
    next: 'Next',
    counter: '{{ index }} / {{ count }}',
  },
  chromas: { label: 'Chromas of {{ skin }}' },
};

// The labels are the API's: the colour family it derived from the accents (ChromaLabel).
const CHROMAS: ChampionChroma[] = [
  {
    id: 1,
    name: 'Ruby',
    label: 'Ruby',
    colors: ['#e0115f', '#9b111e'],
    swatch: 'https://cdn.test/1.png',
  },
  { id: 2, name: 'Pearl', label: 'Pearl', colors: ['#f0ead6'], swatch: 'https://cdn.test/2.png' },
  { id: 3, name: 'Unknown', label: 'Chroma', colors: [], swatch: 'https://cdn.test/3.png' },
];

describe('chromaRamp', () => {
  it('builds a two-stop diagonal from the accent pair', () => {
    expect(chromaRamp(['#ff0000', '#0000ff'])).toBe('linear-gradient(135deg, #ff0000, #0000ff)');
  });

  it('repeats a lone accent so the ramp stays flat instead of breaking', () => {
    expect(chromaRamp(['#ff0000'])).toBe('linear-gradient(135deg, #ff0000, #ff0000)');
  });

  it('falls back to the gold token when a chroma carries no accent', () => {
    expect(chromaRamp([])).toBe('linear-gradient(135deg, var(--color-gold), var(--color-gold))');
  });
});

@Component({
  imports: [ChromaStrip],
  template: '<lodb-chroma-strip skinName="Red Riding Annie" [chromas]="chromas" />',
})
class Host {
  readonly chromas = CHROMAS;
}

describe('lodb-chroma-strip', () => {
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

  function swatches(fixture: ComponentFixture<Host>): HTMLButtonElement[] {
    const element = fixture.nativeElement as HTMLElement;
    return [...element.querySelectorAll<HTMLButtonElement>('button.swatch')];
  }

  it('names the group after the skin and each swatch by the label the API derived', async () => {
    const fixture = await render();
    const group = (fixture.nativeElement as HTMLElement).querySelector('[role="group"]');

    expect(group?.getAttribute('aria-label')).toBe('Chromas of Red Riding Annie');
    expect(swatches(fixture).map((swatch) => swatch.getAttribute('aria-label'))).toEqual([
      'Ruby',
      'Pearl',
      'Chroma',
    ]);
    expect(swatches(fixture)[0]?.title).toBe('Ruby');
  });

  it('rings each swatch with the ramp of its accents', async () => {
    const fixture = await render();
    const ring = (index: number) =>
      swatches(fixture)[index]?.querySelector<HTMLElement>('.swatch__ring')?.getAttribute('style');

    // The DOM reads the colours back in rgb().
    expect(ring(0)).toContain('linear-gradient(135deg, rgb(224, 17, 95), rgb(155, 17, 30))');
    expect(ring(2)).toContain('var(--color-gold)');
  });

  it('opens the viewer on the chroma picked, and steps to the next one', async () => {
    const fixture = await render();

    swatches(fixture)[1]?.click();
    await fixture.whenStable();
    const viewer = document.querySelector('lodb-chroma-viewer') as HTMLElement;
    const art = () => viewer.querySelector('img.art');

    expect(viewer.querySelector('h2')?.textContent?.trim()).toBe('Red Riding Annie');
    expect(art()?.getAttribute('src')).toBe('https://cdn.test/2.png');
    expect(art()?.getAttribute('alt')).toBe('Pearl');

    viewer.querySelector<HTMLButtonElement>('button[aria-label="Next"]')?.click();
    await fixture.whenStable();
    expect(art()?.getAttribute('src')).toBe('https://cdn.test/3.png');
    expect(viewer.textContent).toContain('3 / 3');
  });

  it('steps with the arrow keys pressed where the focus lands, the dialog container', async () => {
    const fixture = await render();
    swatches(fixture)[0]?.click();
    await fixture.whenStable();
    const art = () => document.querySelector('lodb-chroma-viewer img.art')?.getAttribute('src');
    const press = async (key: string) => {
      const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
      document.activeElement?.dispatchEvent(event);
      await fixture.whenStable();
    };

    expect(document.activeElement?.localName).toBe('cdk-dialog-container');
    await press('ArrowRight');
    expect(art()).toBe('https://cdn.test/2.png');
    await press('End');
    expect(art()).toBe('https://cdn.test/3.png');
  });
});
