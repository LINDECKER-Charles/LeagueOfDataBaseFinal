import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
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
  />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class ImageHost {
  readonly src = signal<string | null>('/cdn/Ahri.png');
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
