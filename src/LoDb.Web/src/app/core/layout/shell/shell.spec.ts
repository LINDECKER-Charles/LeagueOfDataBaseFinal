import { Location } from '@angular/common';
import { ApplicationRef, ChangeDetectionStrategy, Component } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { RELEASE_VERSION } from './release-version';
import { Shell } from './shell';

// Slots given in a shuffled order: the envelope, not the caller, decides where they land.
@Component({
  imports: [Shell],
  template: `
    <lodb-shell>
      <p id="page">page</p>
      <span id="contact" lodbSlot="contact">contact</span>
      <span id="banner" lodbSlot="banner">banner</span>
      <span id="account" lodbSlot="account">account</span>
      <span id="switcher" lodbSlot="switcher">switcher</span>
    </lodb-shell>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class ShellHost {}

describe('Shell', () => {
  let lang: string;

  function configure(version: string | null): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'admin', data: { chrome: 'bare' }, children: [{ path: '**', children: [] }] },
          { path: '**', children: [] },
        ]),
        provideTransloco({
          config: { defaultLang: 'en', missingHandler: { logMissingKey: false }, prodMode: true },
          loader: class {
            getTranslation = () => of({});
          },
        }),
        { provide: RELEASE_VERSION, useValue: version },
      ],
    });
  }

  async function render(): Promise<HTMLElement> {
    const fixture: ComponentFixture<ShellHost> = TestBed.createComponent(ShellHost);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
  });

  it('puts the account menu then the context switcher in the header', async () => {
    configure(null);
    const header = (await render()).querySelector('lodb-shell header');
    const account = header?.querySelector('#account');
    const switcher = header?.querySelector('#switcher');

    expect(account).not.toBeNull();
    expect(switcher).not.toBeNull();
    expect(account?.compareDocumentPosition(switcher as Node)).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    );
  });

  it('puts the banner between the header and the page', async () => {
    configure(null);
    const banner = (await render()).querySelector('lodb-shell > #banner');

    expect(banner?.previousElementSibling?.tagName).toBe('LODB-HEADER');
    expect(banner?.nextElementSibling?.tagName).toBe('MAIN');
  });

  it('renders the page, and only the page, in main', async () => {
    configure(null);
    const main = (await render()).querySelector('lodb-shell > main');

    expect(Array.from(main?.children ?? [], (child) => child.id)).toEqual(['page']);
  });

  it('puts the contact entry in the footer', async () => {
    configure(null);
    const footer = (await render()).querySelector('lodb-shell footer');

    expect(footer?.querySelector('#contact')).not.toBeNull();
  });

  it('leaves out the release chip until a release version is provided', async () => {
    configure(null);

    expect((await render()).querySelector('.hx-version-chip')).toBeNull();
  });

  it('links the release chip to the changelog of the page locale', async () => {
    document.documentElement.lang = 'fr';
    configure('2.4.0');
    const chip = (await render()).querySelector('.hx-version-chip');

    expect(chip?.textContent?.trim()).toBe('v2.4.0');
    expect(chip?.getAttribute('href')).toBe('/fr/changelog');
  });

  it('renders a bare section with its page and toasts only, in the default identity', async () => {
    document.cookie = 'lod_theme=noxus; path=/';
    configure(null);
    const host = await render();
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/admin/users');
    await TestBed.inject(ApplicationRef).whenStable();
    const shell = host.querySelector('lodb-shell');
    expect(Array.from(shell?.children ?? [], (child) => child.tagName)).toEqual([
      'LODB-NAVIGATION-PROGRESS',
      'MAIN',
      'LODB-TOASTER',
    ]);
    expect(document.documentElement.getAttribute('data-theme')).toBe('hextech');

    await router.navigateByUrl('/en/champions');
    await TestBed.inject(ApplicationRef).whenStable();
    expect(shell?.querySelector('lodb-header')).not.toBeNull();
    expect(document.documentElement.getAttribute('data-theme')).toBe('noxus');
    document.cookie = 'lod_theme=; path=/; max-age=0';
  });

  it("keeps the chrome out of a bare section's first render, whatever its query", async () => {
    configure(null);
    // The address the browser loaded, before the router has recognized any navigation.
    TestBed.inject(Location).replaceState('/admin', 'range=7');
    const shell = (await render()).querySelector('lodb-shell');

    expect(shell?.querySelector('lodb-header')).toBeNull();
    expect(shell?.querySelector('lodb-footer')).toBeNull();
  });

  it('points every bottom bar destination under the page locale', async () => {
    document.documentElement.lang = 'ar';
    configure(null);
    const links = (await render()).querySelectorAll('.bottom-nav a');

    expect(Array.from(links, (link) => link.getAttribute('href'))).toEqual([
      '/ar',
      '/ar/champions',
      '/ar/items',
      '/ar/runes',
      '/ar/summoners',
      '/ar/trends',
    ]);
    expect(document.documentElement.dir).toBe('rtl');
  });
});
