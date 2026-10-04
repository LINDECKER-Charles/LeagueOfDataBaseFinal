import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { CounterpartLink } from '../../../../../core/api/generated/models/counterpart-link';
import type { Edition } from '../../../../../core/api/generated/models/edition';
import type { PageContext } from '../../../../../core/context/page-context';
import { EditionCounterpart } from './edition-counterpart';

const CONTEXT: PageContext = { locale: 'en', version: '16.19.1', pinned: false, language: 'en_GB' };

@Component({ template: '' })
class Blank {}

@Component({
  imports: [EditionCounterpart],
  template: `<lodb-edition-counterpart
    [edition]="edition()"
    [counterpart]="counterpart()"
    [context]="context"
  />`,
})
class Host {
  readonly edition = signal<Edition>('classic');
  readonly counterpart = signal<CounterpartLink | null>({
    id: '1004',
    name: 'Faerie Charm',
    edition: 'modern',
    canonicalPath: 'items/1004-faerie-charm',
  });
  readonly context = CONTEXT;
}

describe('lodb-edition-counterpart', () => {
  async function render(change?: (host: Host) => void) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', component: Blank }]),
        provideTransloco({
          config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
          loader: class {
            getTranslation = () => of({});
          },
        }),
      ],
    });
    await TestBed.inject(Router).navigateByUrl('/en/items/771004-faerie-charm?lang=en_GB');
    const fixture = TestBed.createComponent(Host);
    change?.(fixture.componentInstance);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('says what a LoL Classic entry is and links its twin, in the same regional variant', async () => {
    const element = await render();
    const link = element.querySelector('a');

    expect(element.textContent).toContain('edition.classic_notice');
    expect(link?.getAttribute('href')).toBe('/en/items/1004-faerie-charm?lang=en_GB');
    expect(link?.dataset['edition']).toBe('modern');
    expect(link?.textContent).toContain('edition.counterpart.modern');
  });

  it('links the LoL Classic twin of a current entry, without the classic notice', async () => {
    const element = await render((host) => {
      host.edition.set('modern');
      host.counterpart.set({
        id: '771004',
        name: 'Faerie Charm',
        edition: 'classic',
        canonicalPath: 'items/771004-faerie-charm',
      });
    });

    expect(element.textContent).not.toContain('edition.classic_notice');
    expect(element.querySelector('a')?.dataset['edition']).toBe('classic');
  });

  it('links nothing when the catalogue does not carry the twin, and says nothing without one', async () => {
    const orphan = await render((host) =>
      host.counterpart.set({ id: '1004', name: 'x', edition: 'modern' }),
    );
    expect(orphan.querySelector('a')).toBeNull();
    expect(orphan.textContent).toContain('edition.classic_notice');

    TestBed.resetTestingModule();
    const current = await render((host) => {
      host.edition.set('modern');
      host.counterpart.set(null);
    });
    expect(current.textContent?.trim()).toBe('');
  });
});
