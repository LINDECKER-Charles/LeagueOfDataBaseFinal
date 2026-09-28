import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { activeSection } from './active-section';
import { FragmentLink } from './fragment-link';
import { Pager } from './pager';
import type { PagerLink } from './pager-link';
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
  imports: [FragmentLink],
  template: `
    <a lodbFragmentLink="pricing">Pricing</a>
    <section id="pricing">Prices</section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class FragmentLinkHost {}

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

@Component({
  imports: [Pager],
  template: `<lodb-pager [previous]="previous" [next]="next" hub="/en/items" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class MarkedPagerHost {
  readonly previous = { url: '/en/items/1001-boots', name: 'Boots' };
  readonly next = {
    url: '/en/items/771004-faerie-charm',
    name: 'Faerie Charm',
    mark: { label: 'LoL Classic', hint: 'League of Legends Classic version' },
  };
}

@Component({
  imports: [Pager],
  template: `<lodb-pager [previous]="previous" [next]="next()" hub="/en/items" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class FittingPagerHost {
  readonly previous = { url: '/en/items/2420-seekers-armguard', name: "Seeker's Armguard" };
  readonly next = signal<PagerLink | null>(null);
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

  it('jumps to a section of the page below its scroll margin, the URL deep-linkable', async () => {
    const fixture = TestBed.createComponent(FragmentLinkHost);
    await fixture.whenStable();
    const link = (fixture.nativeElement as HTMLElement).querySelector('a')!;
    const section = (fixture.nativeElement as HTMLElement).querySelector('section')!;
    const scroll = vi.fn();
    section.scrollIntoView = scroll;
    const push = vi.spyOn(history, 'pushState');

    expect(link.getAttribute('href')).toBe('/#pricing');
    const plain = new MouseEvent('click', { bubbles: true, cancelable: true });
    link.dispatchEvent(plain);
    const modified = new MouseEvent('click', { bubbles: true, cancelable: true, ctrlKey: true });
    link.dispatchEvent(modified);

    expect(plain.defaultPrevented).toBe(true);
    expect(modified.defaultPrevented).toBe(false);
    expect(scroll).toHaveBeenCalledExactlyOnceWith({ behavior: 'auto', block: 'start' });
    expect(push).toHaveBeenCalledExactlyOnceWith(history.state, '', '/#pricing');
    // The next Tab starts inside the section, as after a native `#` link.
    expect(document.activeElement).toBe(section);
    expect(section.getAttribute('tabindex')).toBe('-1');
    push.mockRestore();
  });

  it('keeps the hub in the middle when a neighbour is missing', async () => {
    const fixture = await render();
    const cells = all(fixture, '.pager > *');

    expect(cells.map((cell) => cell.className)).toEqual(['pager__link', 'pager__hub', '']);
    expect(all(fixture, '[rel=prev]')[0]?.getAttribute('href')).toBe('/en/champions/aatrox');
  });

  it('chips the mark of a neighbour after its name, with its hint', async () => {
    const fixture = TestBed.createComponent(MarkedPagerHost);
    await fixture.whenStable();
    const names = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLElement>('.pager__name'),
    );

    expect(names.map((name) => name.textContent?.trim())).toEqual([
      'Boots',
      'Faerie Charm LoL Classic',
    ]);
    const chip = names[1]?.querySelector('.hx-chip-hex');
    expect(chip?.getAttribute('title')).toBe('League of Legends Classic version');
    expect(names[0]?.querySelector('.hx-chip-hex')).toBeNull();
  });

  it('turns tight only where its links would overflow it, measured with each neighbour', async () => {
    const fixture = TestBed.createComponent(FittingPagerHost);
    await fixture.whenStable();
    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav') as HTMLElement;
    // jsdom lays nothing out: the widths a 390px phone gives a pair of long names.
    const widths = { scroll: 415, client: 342 };
    Object.defineProperty(nav, 'scrollWidth', { get: () => widths.scroll });
    Object.defineProperty(nav, 'clientWidth', { get: () => widths.client });

    fixture.componentInstance.next.set({ url: '/en/items/2421-x', name: 'Shattered Armguard' });
    await fixture.whenStable();
    expect(nav.classList).toContain('pager--tight');

    widths.scroll = widths.client;
    fixture.componentInstance.next.set({ url: '/en/items/2422-x', name: 'Boots' });
    await fixture.whenStable();
    expect(nav.classList).not.toContain('pager--tight');
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
