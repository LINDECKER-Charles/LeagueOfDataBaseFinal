import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, PLATFORM_ID } from '@angular/core';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { of } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-base-url';
import type { CatalogMeta } from '../../core/api/generated/models/catalog-meta';
import { PreferencesStore } from '../../core/context/preferences/preferences-store';
import { WarmUpLoader } from '../../core/context/warm-up/warm-up-loader';
import type { WarmUpTarget } from '../../core/context/warm-up/warm-up-target';
import { NavContext } from '../../core/layout/nav/nav-context';
import { ToastService } from '../../core/layout/toast/toast-service';
import { ContextSwitcher } from './context-switcher';

@Component({ template: '' })
class Probe {}

const ORIGIN = 'http://api.test';
const META_URL = `${ORIGIN}/api/meta`;
const LATEST = '16.19.1';
const OLDER = '15.14.1';
const META: CatalogMeta = {
  latest: LATEST,
  versions: [LATEST, OLDER],
  readyVersions: [LATEST],
  versionPattern: String.raw`\d+(?:\.\d+)+`,
  languages: ['en_US', 'en_GB', 'fr_FR'],
  languagePattern: '[a-z]{2}_[A-Z]{2}',
  defaultLanguage: 'en_US',
  locales: [
    { locale: 'en', language: 'en_US' },
    { locale: 'fr', language: 'fr_FR' },
  ],
  fallbackLocale: 'en',
  gameModes: [{ mode: 'sr', map: 11 }],
  defaultGameMode: 'sr',
};
const FORGET = 'lod_prefs=; path=/; max-age=0';
// Lets every visit through at once: the loader's own spec covers the wait.
const gate = vi.fn((_target: WarmUpTarget, visit: () => Promise<boolean>) => visit());

function configure(platform: 'browser' | 'server'): void {
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: platform },
      { provide: API_BASE_URL, useValue: ORIGIN },
      { provide: WarmUpLoader, useValue: { gate } },
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([{ path: ':locale', children: [{ path: '**', component: Probe }] }]),
      provideTransloco({
        config: {
          availableLangs: ['en', 'fr'],
          defaultLang: 'en',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = () => of({});
        },
      }),
    ],
  });
}

// The page the switcher sits on: the locale resolver writes <html lang> before it renders.
async function openOn(url: string): Promise<ComponentFixture<ContextSwitcher>> {
  document.documentElement.lang = url.split('/')[1];
  await TestBed.inject(Router).navigateByUrl(url);
  const fixture = TestBed.createComponent(ContextSwitcher);
  await fixture.whenStable();
  return fixture;
}

async function loaded(fixture: ComponentFixture<ContextSwitcher>): Promise<HTMLElement> {
  TestBed.inject(HttpTestingController).expectOne(META_URL).flush(META);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

function select(host: HTMLElement, id: string): HTMLSelectElement {
  return host.querySelector<HTMLSelectElement>(`#${id}`) as HTMLSelectElement;
}

function pick(host: HTMLElement, id: string, value: string): void {
  const control = select(host, id);
  control.value = value;
  control.dispatchEvent(new Event('change'));
}

async function submit(fixture: ComponentFixture<ContextSwitcher>): Promise<void> {
  const form = (fixture.nativeElement as HTMLElement).querySelector('form') as HTMLFormElement;
  form.dispatchEvent(new Event('submit', { cancelable: true }));
  await fixture.whenStable();
}

function routerUrl(): string {
  return TestBed.inject(Router).url;
}

describe('ContextSwitcher', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
    document.cookie = FORGET;
    gate.mockClear();
  });

  afterEach(() => {
    sessionStorage.clear();
    document.cookie = FORGET;
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
  });

  it('asks nothing of the API while rendering on the server', async () => {
    configure('server');

    const fixture = await openOn('/fr/champions');

    TestBed.inject(HttpTestingController).expectNone(META_URL);
    expect(select(fixture.nativeElement, 'switcher-version').options).toHaveLength(0);
    expect(fixture.nativeElement.querySelector('summary').textContent).toContain('FR');
  });

  it('loads the versions and languages in the browser, the page context selected', async () => {
    configure('browser');
    const fixture = await openOn(`/fr/${OLDER}/champions`);

    const host = await loaded(fixture);

    const versions = [...select(host, 'switcher-version').options].map((option) => option.value);
    const languages = [...select(host, 'switcher-language').options].map((option) => option.value);
    expect(versions).toEqual([LATEST, OLDER]);
    expect(languages).toEqual(['en:en_US', 'en:en_GB', 'fr:fr_FR']);
    expect(select(host, 'switcher-version').value).toBe(OLDER);
    expect(select(host, 'switcher-language').value).toBe('fr:fr_FR');
    expect(host.querySelector('summary')?.textContent).toContain(OLDER);
  });

  // The patch leaves the chip on narrow screens (heritage H6): its name still carries both.
  it('names the chip after the patch and the language it shows', async () => {
    configure('browser');
    const fixture = await openOn(`/fr/${OLDER}/champions`);
    const summary = (): string | null =>
      (fixture.nativeElement as HTMLElement).querySelector('summary')?.getAttribute('aria-label') ??
      null;

    expect(summary()).toMatch(/ — FR$/);

    await loaded(fixture);

    expect(summary()).toContain(` — ${OLDER}, FR`);
  });

  it('navigates to the rewritten path without remembering by default', async () => {
    configure('browser');
    const fixture = await openOn('/fr/items?page=2');
    const host = await loaded(fixture);

    pick(host, 'switcher-version', OLDER);
    pick(host, 'switcher-language', 'en:en_GB');
    await submit(fixture);

    expect(routerUrl()).toBe(`/en/${OLDER}/items?page=2&lang=en_GB`);
    expect(document.cookie).not.toContain('lod_prefs');
  });

  it('warms the chosen patch and language, and the lists of the page, before the visit', async () => {
    configure('browser');
    const fixture = await openOn('/fr/items?page=2');
    const host = await loaded(fixture);

    pick(host, 'switcher-version', OLDER);
    pick(host, 'switcher-language', 'en:en_GB');
    await submit(fixture);
    await submit(fixture);

    // The second choice changes nothing: that visit needs no loader.
    expect(gate).toHaveBeenCalledExactlyOnceWith(
      { version: OLDER, language: 'en_GB', resources: ['items'] },
      expect.any(Function),
    );
    expect(routerUrl()).toBe(`/en/${OLDER}/items?page=2&lang=en_GB`);
  });

  it('confirms every choice with a toast, as the legacy flash did', async () => {
    configure('browser');
    const fixture = await openOn('/fr/items');
    const host = await loaded(fixture);
    const saved = () =>
      TestBed.inject(ToastService)
        .toasts()
        .map((toast) => toast.message);

    pick(host, 'switcher-version', OLDER);
    await submit(fixture);
    await vi.waitFor(() => expect(saved()).toEqual(['contextSwitcher.saved']));

    await submit(fixture);
    await vi.waitFor(() => expect(saved()).toHaveLength(2));
    expect(routerUrl()).toBe(`/fr/${OLDER}/items`);
  });

  it('writes lod_prefs when "remember" is ticked', async () => {
    configure('browser');
    const fixture = await openOn('/en/');
    const host = await loaded(fixture);

    pick(host, 'switcher-version', OLDER);
    const remember = host.querySelector<HTMLInputElement>('#switcher-remember');
    remember?.click();
    await submit(fixture);

    expect(document.cookie).toContain(`lod_prefs=loc=en&v=${OLDER}`);
    expect(routerUrl()).toBe(`/en/?version=${OLDER}`);
  });

  it('forgets lod_prefs when "remember" is unticked', async () => {
    document.cookie = `lod_prefs=v=${OLDER}; path=/`;
    configure('browser');
    const fixture = await openOn(`/en/${OLDER}/runes`);
    const host = await loaded(fixture);

    expect(host.querySelector<HTMLInputElement>('#switcher-remember')?.checked).toBe(true);
    host.querySelector<HTMLInputElement>('#switcher-remember')?.click();
    pick(host, 'switcher-version', LATEST);
    await submit(fixture);

    expect(document.cookie).not.toContain('lod_prefs');
    expect(routerUrl()).toBe('/en/runes');
  });

  it('applies the remembered context to a page whose URL names none', async () => {
    document.cookie = `lod_prefs=l=en_GB&v=${OLDER}; path=/`;
    configure('browser');
    const fixture = await openOn('/en/champions');

    await loaded(fixture);

    expect(routerUrl()).toBe(`/en/${OLDER}/champions?lang=en_GB`);
  });

  it('keeps a choice for the session, remember unticked, for the next page', async () => {
    configure('browser');
    const fixture = await openOn('/en/items');
    const host = await loaded(fixture);
    pick(host, 'switcher-version', OLDER);
    await submit(fixture);

    await TestBed.inject(Router).navigateByUrl('/en/champions');
    await fixture.whenStable();

    expect(document.cookie).not.toContain('lod_prefs');
    await vi.waitFor(() => expect(routerUrl()).toBe(`/en/${OLDER}/champions`));
  });

  // The legacy `page_selection`: the chip and the panel follow the session like the links.
  it('shows the patch kept for the session on a page that names none', async () => {
    configure('browser');
    TestBed.inject(PreferencesStore).keep({ lang: null, version: OLDER });
    const fixture = await openOn('/en/trends');

    const host = await loaded(fixture);

    expect(host.querySelector('summary')?.textContent).toContain(OLDER);
    expect(select(host, 'switcher-version').value).toBe(OLDER);
  });

  it('moves the chip and the chrome to a patch applied on the same URL', async () => {
    configure('browser');
    TestBed.inject(PreferencesStore).keep({ lang: null, version: OLDER });
    const fixture = await openOn('/en/trends');
    const host = await loaded(fixture);
    const nav = TestBed.inject(NavContext);
    expect(nav.selection()?.version).toBe(OLDER);

    pick(host, 'switcher-version', LATEST);
    await submit(fixture);

    expect(routerUrl()).toBe('/en/trends');
    expect(host.querySelector('summary')?.textContent).toContain(LATEST);
    expect(nav.selection()).toEqual({
      shown: LATEST,
      version: null,
      lang: null,
    });
  });

  it('leaves a page whose URL names its context', async () => {
    document.cookie = `lod_prefs=v=${OLDER}; path=/`;
    configure('browser');
    const fixture = await openOn('/en/champions?version=16.19.1');

    await loaded(fixture);

    expect(routerUrl()).toBe('/en/champions?version=16.19.1');
  });

  it('says so when /api/meta cannot be read, the form disabled', async () => {
    configure('browser');
    const fixture = await openOn('/en/');

    TestBed.inject(HttpTestingController)
      .expectOne(META_URL)
      .flush(null, { status: 503, statusText: 'Service Unavailable' });
    await fixture.whenStable();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[role="status"]')?.textContent).toContain(
      'contextSwitcher.unavailable',
    );
    expect(host.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);
  });
});
