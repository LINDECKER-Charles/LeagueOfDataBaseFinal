import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { ImageVariant } from '../../core/api/generated/models/image-variant';
import { Image } from './image';
import { imageStateOf } from './image-state-of';
import { webpSource } from './webp-source';

describe('imageStateOf', () => {
  it.each([
    [false, 0, 'loading'],
    [true, 120, 'loaded'],
    [true, 0, 'error'],
  ] as const)('reads complete=%s and width %i as %s', (complete, width, expected) => {
    expect(imageStateOf(complete, width)).toBe(expected);
  });
});

describe('webpSource', () => {
  it('derives the WebP twin of a PNG', () => {
    expect(webpSource('/cdn/16.1.1/img/champion/Ahri.png')).toBe(
      '/cdn/16.1.1/img/champion/Ahri.webp',
    );
  });

  it.each(['/cdn/splash/Ahri_0.jpg', '/cdn/icon.PNG', '/cdn/icon.png.txt'])(
    'gives %s no twin',
    (source) => {
      expect(webpSource(source)).toBeNull();
    },
  );
});

@Component({
  imports: [Image],
  template: `<lodb-image
    class="size-16"
    [src]="src()"
    alt="Ahri"
    [width]="64"
    [height]="64"
    initials="AH"
    [eager]="eager()"
    [priority]="priority()"
    [variants]="variants()"
    [sizes]="sizes()"
  />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class ImageHost {
  readonly src = signal<string | null>('/cdn/Ahri.png');
  readonly eager = signal(false);
  readonly priority = signal(false);
  readonly variants = signal<readonly ImageVariant[]>([]);
  readonly sizes = signal<string | null>(null);
}

describe('Image', () => {
  async function render(): Promise<ComponentFixture<ImageHost>> {
    const fixture = TestBed.createComponent(ImageHost);
    await fixture.whenStable();
    return fixture;
  }

  function query(fixture: ComponentFixture<ImageHost>, selector: string): Element | null {
    return (fixture.nativeElement as HTMLElement).querySelector(selector);
  }

  it('offers the WebP twin before the original, with the box reserved', async () => {
    const fixture = await render();

    expect(query(fixture, 'source')?.getAttribute('srcset')).toBe('/cdn/Ahri.webp');
    expect(query(fixture, 'img')?.getAttribute('width')).toBe('64');
    expect(query(fixture, 'img')?.getAttribute('loading')).toBe('lazy');
    expect(query(fixture, 'img')?.hasAttribute('fetchpriority')).toBe(false);
  });

  it('loads an image above the fold at once, at high priority for the LCP candidate', async () => {
    const fixture = await render();
    const img = () => query(fixture, 'img');

    fixture.componentInstance.eager.set(true);
    await fixture.whenStable();
    expect(img()?.getAttribute('loading')).toBe('eager');
    expect(img()?.hasAttribute('fetchpriority')).toBe(false);

    fixture.componentInstance.eager.set(false);
    fixture.componentInstance.priority.set(true);
    await fixture.whenStable();
    expect(img()?.getAttribute('loading')).toBe('eager');
    expect(img()?.getAttribute('fetchpriority')).toBe('high');
  });

  it('offers the stored variants of an art, the hotlinked source as fallback', async () => {
    const fixture = await render();

    fixture.componentInstance.src.set('https://ddragon.example/Ahri_0.jpg');
    fixture.componentInstance.variants.set([
      { url: '/cdn/blobs/a.webp', width: 480 },
      { url: '/cdn/blobs/b.webp', width: 960 },
    ]);
    fixture.componentInstance.sizes.set('100vw');
    await fixture.whenStable();

    const source = query(fixture, 'source');
    expect(source?.getAttribute('srcset')).toBe('/cdn/blobs/a.webp 480w, /cdn/blobs/b.webp 960w');
    expect(source?.getAttribute('sizes')).toBe('100vw');
    expect(query(fixture, 'img')?.getAttribute('src')).toBe('https://ddragon.example/Ahri_0.jpg');
  });

  it('stamps the state reported by the image events', async () => {
    const fixture = await render();
    const img = query(fixture, 'img') as HTMLImageElement;

    img.dispatchEvent(new Event('error'));
    await fixture.whenStable();

    expect(img.getAttribute('data-img-state')).toBe('error');
  });

  it('renders the initials box for an absent image', async () => {
    const fixture = await render();

    fixture.componentInstance.src.set(null);
    await fixture.whenStable();

    expect(query(fixture, 'img')).toBeNull();
    expect(query(fixture, '.hx-img-initials')?.textContent).toBe('AH');
  });
});
