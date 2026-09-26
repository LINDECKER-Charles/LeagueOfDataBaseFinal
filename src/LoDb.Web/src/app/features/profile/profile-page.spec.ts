import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, type Translation } from '@jsverse/transloco';
import { of } from 'rxjs';
import type { PublicProfile } from '../../core/api/generated/models/public-profile';
import { CANONICAL_ORIGIN } from '../../core/seo/canonical-origin';
import { Seo } from '../../core/seo/seo';
import { ProfilePage } from './profile-page';
import { publicProfileOf } from './testing/public-profile-of';

const ORIGIN = 'https://league-of-data-base.com';
const CATALOGUES: Record<string, Translation> = {
  fr: {
    community: { supporter: { badge: 'Soutien' } },
    header: { navigation: { home: 'Accueil' } },
    profile: {
      identity: { member_since: 'Membre depuis' },
      public: {
        eyebrow: 'Carte d’invocateur',
        description: 'La carte de {{ username }}.',
        builds_title: 'Builds publics',
        no_builds: 'Aucun build public.',
      },
    },
  },
};

async function visit(profile: PublicProfile) {
  document.documentElement.lang = 'fr';
  TestBed.configureTestingModule({
    providers: [
      { provide: CANONICAL_ORIGIN, useValue: ORIGIN },
      provideRouter([
        {
          path: ':locale/u/:username',
          resolve: { profile: () => profile },
          component: ProfilePage,
        },
      ]),
      provideTransloco({
        config: {
          availableLangs: ['en', 'fr'],
          defaultLang: 'fr',
          missingHandler: { logMissingKey: false },
          prodMode: true,
        },
        loader: class {
          getTranslation = (path: string) => of(CATALOGUES[path] ?? {});
        },
      }),
    ],
  });
  const apply = vi.spyOn(TestBed.inject(Seo), 'apply');
  const harness = await RouterTestingHarness.create(`/fr/u/${profile.username}`);
  await harness.fixture.whenStable();
  return { apply, host: harness.routeNativeElement as HTMLElement };
}

function textOf(host: HTMLElement, selector: string): string {
  return textsOf(host, selector)[0] ?? '';
}

function textsOf(host: HTMLElement, selector: string): string[] {
  return [...host.querySelectorAll(selector)].map(
    (element) => element.textContent?.replace(/\s+/g, ' ').trim() ?? '',
  );
}

describe('ProfilePage', () => {
  let lang: string;

  beforeEach(() => {
    lang = document.documentElement.lang;
  });

  afterEach(() => {
    document.documentElement.lang = lang;
    document.documentElement.removeAttribute('dir');
    document.head.querySelectorAll('[data-lodb-seo]').forEach((element) => element.remove());
  });

  it('shows the name, the supporter seal, the day it joined and the skin over its splash', async () => {
    const { host } = await visit(publicProfileOf());

    expect(textOf(host, 'h1')).toBe('Faker#KR1');
    expect(host.querySelector('h1 [role="img"]')?.getAttribute('aria-label')).toBe('Soutien');
    expect(textsOf(host, '.profile-hero__meta > span')).toEqual([
      'Membre depuis · 5 mars 2024',
      'Arcade Ahri',
    ]);
    expect(host.querySelector('.profile-hero__art')?.getAttribute('src')).toBe(
      '/cdn/ahri-7-centered.jpg',
    );
    expect(host.querySelectorAll('.profile-orb')).toHaveLength(3);
  });

  it('falls back on the wide splash when the centered one fails', async () => {
    const { host } = await visit(publicProfileOf());
    const art = host.querySelector<HTMLImageElement>('.profile-hero__art');

    art?.dispatchEvent(new Event('error'));

    expect(art?.getAttribute('src')).toBe('/cdn/ahri-7.jpg');
  });

  it('shows neither seal nor splash for a plain card', async () => {
    const card = publicProfileOf({ isSupporter: false, backdrop: null });

    const { host } = await visit({ ...card, showcase: { ...card.showcase, skin: null } });

    expect(host.querySelector('h1 [role="img"]')).toBeNull();
    expect(host.querySelector('.profile-hero__art')).toBeNull();
    expect(textsOf(host, '.profile-hero__meta > span')).toEqual(['Membre depuis · 5 mars 2024']);
  });

  it('links each public build to its shared page, the champion named by its id when unknown', async () => {
    const { host } = await visit(publicProfileOf());
    const links = [...host.querySelectorAll<HTMLAnchorElement>('.profile-build')];

    expect(textOf(host, 'h2')).toBe('Builds publics');
    expect(textOf(host, '.codex-header__meta')).toBe('2');
    expect(links.map((link) => link.getAttribute('href'))).toEqual(['/b/k3y-1', '/b/k3y-2']);
    expect(textsOf(host, '.profile-build__name')).toEqual(['Mid burst', 'Old times']);
    expect(textsOf(host, '.profile-build__meta')).toEqual(['Ahri · 16.19.1', 'Zeri · 11.1.1']);
    expect(textOf(links[1], '.profile-build__portrait')).toBe('ZE');
  });

  it('says so when no build is public', async () => {
    const { host } = await visit(publicProfileOf({ builds: [] }));

    expect(host.querySelector('.profile-build')).toBeNull();
    expect(host.textContent).toContain('Aucun build public.');
  });

  it('writes an indexable head for the card, in its locale', async () => {
    const { apply } = await visit(publicProfileOf());

    expect(apply).toHaveBeenLastCalledWith(
      expect.objectContaining({
        title: 'Faker#KR1',
        description: 'La carte de Faker#KR1.',
        path: 'u/Faker',
        locale: 'fr',
        ogType: 'profile',
        image: '/cdn/ahri-7.jpg',
      }),
    );
    expect(document.head.querySelector('link[rel="canonical"]')?.getAttribute('href')).toBe(
      `${ORIGIN}/fr/u/Faker`,
    );
  });
});
