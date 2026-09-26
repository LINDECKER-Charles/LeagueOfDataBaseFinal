import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { activeSection } from './active-section';
import { Pager } from './pager';
import { SectionNav } from './section-nav';

describe('activeSection', () => {
  const order = ['abilities', 'skins', 'lore'];

  it('marks the first crossing section in document order', () => {
    expect(activeSection(order, new Set(['lore', 'skins']), 'abilities')).toBe('skins');
  });

  it('keeps the previous mark when no section crosses the band', () => {
    expect(activeSection(order, new Set(), 'skins')).toBe('skins');
  });

  it('ignores a crossing element that is not in the nav', () => {
    expect(activeSection(order, new Set(['footer']), undefined)).toBeUndefined();
  });
});

@Component({
  imports: [Pager, SectionNav],
  template: `
    <lodb-section-nav label="Sections" [sections]="sections" />
    <lodb-pager [previous]="{ url: '/en/champions/aatrox', name: 'Aatrox' }" hub="/en/champions" />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class NavigationHost {
  readonly sections = [
    { id: 'abilities', label: 'Abilities' },
    { id: 'skins', label: 'Skins' },
  ];
}

@Component({
  imports: [Pager],
  template: `<lodb-pager [next]="next" [hub]="hub" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class RegionalPagerHost {
  private readonly router = inject(Router);
  readonly next = { url: this.router.parseUrl('/en/champions/akali?lang=en_GB'), name: 'Akali' };
  readonly hub = this.router.parseUrl('/en/champions?lang=en_GB');
}

describe('navigation primitives', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'IntersectionObserver',
      class {
        observe = vi.fn();
        disconnect = vi.fn();
      },
    );
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
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  async function render(): Promise<ComponentFixture<NavigationHost>> {
    const fixture = TestBed.createComponent(NavigationHost);
    await fixture.whenStable();
    return fixture;
  }

  function all(fixture: ComponentFixture<NavigationHost>, selector: string): HTMLElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll(selector));
  }

  it('links each chip to its section, the first one current at the top of the page', async () => {
    const chips = all(await render(), '.section-nav a');

    expect(chips.map((chip) => chip.getAttribute('href'))).toEqual(['/#abilities', '/#skins']);
    expect(chips.map((chip) => chip.getAttribute('aria-current'))).toEqual(['true', null]);
  });

  it('keeps the hub in the middle when a neighbour is missing', async () => {
    const fixture = await render();
    const cells = all(fixture, '.pager > *');

    expect(cells.map((cell) => cell.className)).toEqual(['pager__link', 'pager__hub', '']);
    expect(all(fixture, '[rel=prev]')[0]?.getAttribute('href')).toBe('/en/champions/aatrox');
  });

  it('carries the regional variant of UrlTree links, query unescaped', async () => {
    const fixture = TestBed.createComponent(RegionalPagerHost);
    await fixture.whenStable();
    const hrefs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('.pager a'));

    expect(hrefs.map((link) => link.getAttribute('href'))).toEqual([
      '/en/champions?lang=en_GB',
      '/en/champions/akali?lang=en_GB',
    ]);
  });
});
