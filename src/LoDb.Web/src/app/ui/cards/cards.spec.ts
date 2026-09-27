import { Component, inject, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { CatalogImage } from '../../core/api/generated/models/catalog-image';
import type { Edition } from '../../core/api/generated/models/edition';
import { EntityCard } from './entity-card';
import { initialsOf } from './initials-of';

describe('initialsOf', () => {
  it('takes the first two letters, in capitals', () => {
    expect(initialsOf(' long sword')).toBe('LO');
    expect(initialsOf('É')).toBe('É');
  });
});

@Component({
  imports: [EntityCard],
  template: `<lodb-entity-card
    [href]="href"
    name="Long Sword"
    caption="350"
    [image]="image()"
    [edition]="edition()"
  />`,
})
class Host {
  readonly href = inject(Router).parseUrl('/en/items/1036-long-sword?lang=en_GB');
  readonly image = signal<CatalogImage>({ status: 'present', url: '/cdn/blobs/a.png' });
  readonly edition = signal<Edition>('modern');
}

describe('lodb-entity-card', () => {
  async function render() {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideTransloco({
          config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
          loader: class {
            getTranslation = () => of({});
          },
        }),
      ],
    });
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element };
  }

  it('links the entry, with its art, its name and its caption', async () => {
    const { element } = await render();
    expect(element.querySelector('a')?.getAttribute('href')).toBe(
      '/en/items/1036-long-sword?lang=en_GB',
    );
    expect(element.querySelector('img')?.getAttribute('src')).toBe('/cdn/blobs/a.png');
    expect(element.querySelector('h3')?.textContent?.trim()).toBe('Long Sword');
    expect(element.textContent).toContain('350');
  });

  it('sweeps a pending image and shows the initials of an absent one', async () => {
    const { fixture, element } = await render();
    fixture.componentInstance.image.set({ status: 'pending' });
    await fixture.whenStable();
    expect(element.querySelector('.hx-sk')).not.toBeNull();
    expect(element.querySelector('img')).toBeNull();
    fixture.componentInstance.image.set({ status: 'absent' });
    await fixture.whenStable();
    expect(element.querySelector('.hx-img-initials')?.textContent).toBe('LO');
  });

  it('marks the LoL Classic edition only', async () => {
    const { fixture, element } = await render();
    expect(element.querySelector('.hx-chip-hex')).toBeNull();
    fixture.componentInstance.edition.set('classic');
    await fixture.whenStable();
    expect(element.querySelector('.hx-chip-hex')).not.toBeNull();
  });
});
